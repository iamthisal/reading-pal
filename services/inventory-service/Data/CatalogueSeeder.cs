using InventoryService.Models;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Data
{
    /// <summary>
    /// Gives a fresh catalogue some starter content so the sections on the home page are not empty:
    /// an author entry for every author already on a book, and three lists. It only fills tables
    /// that are completely empty, so anything an admin has set up is left alone.
    /// </summary>
    public static class CatalogueSeeder
    {
        public const string RecommendedKey = "recommended";
        public const string Top100Key = "top-100";
        public const string PopularAuthorsKey = "popular-authors";

        public static async Task SeedAsync(InventoryDbContext db)
        {
            var books = await db.Books.OrderByDescending(book => book.CreatedAt).ThenByDescending(book => book.Id).ToListAsync();

            if (!await db.Authors.AnyAsync())
            {
                var names = books
                    .Select(book => book.Author.Trim())
                    .Where(name => name.Length > 0)
                    .GroupBy(name => name.ToLowerInvariant())
                    // Where the same author is spelled with different capitals, keep the most common spelling.
                    .Select(group => group.GroupBy(name => name)
                        .OrderByDescending(spelling => spelling.Count())
                        .ThenBy(spelling => spelling.Key, StringComparer.Ordinal)
                        .First().Key);
                db.Authors.AddRange(names.Select(name => new Author { Name = name }));
                await db.SaveChangesAsync();
            }

            if (await db.CuratedLists.AnyAsync())
            {
                return;
            }

            var recommended = new CuratedList { Key = RecommendedKey, Name = "Recommended for you", ItemType = CuratedListItemType.Books };
            var top100 = new CuratedList { Key = Top100Key, Name = "Top 100", ItemType = CuratedListItemType.Books };
            var popularAuthors = new CuratedList { Key = PopularAuthorsKey, Name = "Popular Authors", ItemType = CuratedListItemType.Authors };

            // Books that can be reserved right now make the better first impression.
            AddBooks(recommended, books.OrderByDescending(book => book.AvailableCopies > 0).Take(6));
            AddBooks(top100, books.Take(100));

            var bookCounts = books
                .GroupBy(book => book.Author.Trim().ToLowerInvariant())
                .ToDictionary(group => group.Key, group => group.Count());
            var authors = await db.Authors.ToListAsync();
            var position = 0;
            foreach (var author in authors
                .OrderByDescending(author => bookCounts.GetValueOrDefault(author.Name.Trim().ToLowerInvariant()))
                .ThenBy(author => author.Name)
                .Take(6))
            {
                popularAuthors.Items.Add(new CuratedListItem { AuthorId = author.Id, Position = position++ });
            }

            db.CuratedLists.AddRange(recommended, top100, popularAuthors);
            await db.SaveChangesAsync();
        }

        private static void AddBooks(CuratedList list, IEnumerable<Book> books)
        {
            var position = 0;
            foreach (var book in books)
            {
                list.Items.Add(new CuratedListItem { BookId = book.Id, Position = position++ });
            }
        }
    }
}

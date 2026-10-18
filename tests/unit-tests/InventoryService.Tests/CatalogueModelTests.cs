using System;
using System.Linq;
using System.Threading.Tasks;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class CatalogueModelTests
    {
        [Fact]
        public void Author_DefaultValues_AreProperlyInitialized()
        {
            var author = new Author();

            Assert.Equal(0, author.Id);
            Assert.Equal(string.Empty, author.Name);
            Assert.True(author.CreatedAt <= DateTime.UtcNow);
            Assert.True(author.UpdatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void CuratedList_DefaultValues_AreProperlyInitialized()
        {
            var list = new CuratedList();

            Assert.Equal(0, list.Id);
            Assert.Equal(string.Empty, list.Key);
            Assert.Equal(string.Empty, list.Name);
            Assert.Equal(CuratedListItemType.Books, list.ItemType);
            Assert.Empty(list.Items);
            Assert.True(list.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void CuratedListItem_DefaultValues_PointAtNothing()
        {
            var item = new CuratedListItem();

            Assert.Equal(0, item.Position);
            Assert.Null(item.BookId);
            Assert.Null(item.AuthorId);
        }

        [Fact]
        public async Task Author_Names_Must_Be_Unique()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Authors.AddRange(new Author { Name = "Jane Austen" }, new Author { Name = "Jane Austen" });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task List_Keys_Must_Be_Unique()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.CuratedLists.AddRange(new CuratedList { Key = "top-100", Name = "A" }, new CuratedList { Key = "top-100", Name = "B" });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task A_Book_Cannot_Be_On_The_Same_List_Twice()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            var list = new CuratedList { Key = "classics", Name = "Classics" };
            context.AddRange(book, list);
            await context.SaveChangesAsync();
            context.CuratedListItems.AddRange(
                new CuratedListItem { CuratedListId = list.Id, BookId = book.Id, Position = 0 },
                new CuratedListItem { CuratedListId = list.Id, BookId = book.Id, Position = 1 });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task List_ItemType_Is_Stored_As_Text()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using (var context = factory.CreateContext())
            {
                context.CuratedLists.Add(new CuratedList { Key = "popular-authors", Name = "Popular Authors", ItemType = CuratedListItemType.Authors });
                await context.SaveChangesAsync();
            }

            using var check = factory.CreateContext();
            var stored = await check.Database.SqlQueryRaw<string>("SELECT ItemType AS Value FROM CuratedLists").SingleAsync();
            Assert.Equal("Authors", stored);
        }

        [Fact]
        public async Task Deleting_A_Book_Removes_It_From_Its_Lists()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            int listId;
            using (var context = factory.CreateContext())
            {
                var kept = CatalogueTestData.Book("Kept");
                var removed = CatalogueTestData.Book("Removed");
                var list = new CuratedList { Key = "top-100", Name = "Top 100" };
                context.AddRange(kept, removed, list);
                await context.SaveChangesAsync();
                context.CuratedListItems.AddRange(
                    new CuratedListItem { CuratedListId = list.Id, BookId = removed.Id, Position = 0 },
                    new CuratedListItem { CuratedListId = list.Id, BookId = kept.Id, Position = 1 });
                await context.SaveChangesAsync();
                listId = list.Id;

                context.Books.Remove(removed);
                await context.SaveChangesAsync();
            }

            using var check = factory.CreateContext();
            var item = await check.CuratedListItems.Include(i => i.Book).SingleAsync(i => i.CuratedListId == listId);
            Assert.Equal("Kept", item.Book!.Title);
        }

        [Fact]
        public async Task Deleting_A_List_Removes_Its_Items_But_Not_The_Books()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using (var context = factory.CreateContext())
            {
                var book = CatalogueTestData.Book("Emma");
                var list = new CuratedList { Key = "classics", Name = "Classics" };
                context.AddRange(book, list);
                await context.SaveChangesAsync();
                context.CuratedListItems.Add(new CuratedListItem { CuratedListId = list.Id, BookId = book.Id });
                await context.SaveChangesAsync();

                context.CuratedLists.Remove(list);
                await context.SaveChangesAsync();
            }

            using var check = factory.CreateContext();
            Assert.Empty(await check.CuratedListItems.ToListAsync());
            Assert.Single(await check.Books.ToListAsync());
        }
    }
}

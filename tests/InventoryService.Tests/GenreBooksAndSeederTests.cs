using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class GenreBooksTests
    {
        [Fact]
        public async Task GetBooks_Returns_Only_That_Categorys_Books_Newest_First()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var genre = new Genre { Name = "History" };
            context.Genres.Add(genre);
            context.Books.AddRange(
                CatalogueTestData.Book("Older", genre: "History", createdAt: new DateTime(2026, 1, 1)),
                CatalogueTestData.Book("Newer", genre: "history", createdAt: new DateTime(2026, 2, 1)),
                CatalogueTestData.Book("Other", genre: "Fiction"));
            await context.SaveChangesAsync();

            var result = await new GenresController(context).GetBooks(genre.Id);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var books = Assert.IsAssignableFrom<IEnumerable<BookResponse>>(ok.Value).ToList();
            Assert.Equal(new[] { "Newer", "Older" }, books.Select(b => b.Title));
        }

        [Fact]
        public async Task GetBooks_Respects_Limit()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var genre = new Genre { Name = "History" };
            context.Genres.Add(genre);
            context.Books.AddRange(Enumerable.Range(1, 4).Select(n => CatalogueTestData.Book($"Book {n}", genre: "History")));
            await context.SaveChangesAsync();

            var result = await new GenresController(context).GetBooks(genre.Id, limit: 3);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(3, Assert.IsAssignableFrom<IEnumerable<BookResponse>>(ok.Value).Count());
        }

        [Fact]
        public async Task GetBooks_Unknown_Category_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new GenresController(context).GetBooks(99);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }
    }

    public class CatalogueSeederTests
    {
        [Fact]
        public async Task Seed_Creates_An_Author_For_Each_Distinct_Book_Author()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Books.AddRange(
                CatalogueTestData.Book("Emma", "Jane Austen"),
                CatalogueTestData.Book("Persuasion", " jane austen "),
                CatalogueTestData.Book("Swing Time", "Zadie Smith"));
            await context.SaveChangesAsync();

            await CatalogueSeeder.SeedAsync(context);

            Assert.Equal(new[] { "Jane Austen", "Zadie Smith" }, (await context.Authors.OrderBy(a => a.Name).ToListAsync()).Select(a => a.Name));
        }

        [Fact]
        public async Task Seed_Creates_The_Three_Starter_Lists()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Books.AddRange(Enumerable.Range(1, 8).Select(n => CatalogueTestData.Book($"Book {n}", n <= 5 ? "Busy Author" : $"Author {n}")));
            context.Books.Add(CatalogueTestData.Book("Out of stock", "Author 9", available: 0, createdAt: DateTime.UtcNow.AddDays(1)));
            await context.SaveChangesAsync();

            await CatalogueSeeder.SeedAsync(context);

            var lists = await context.CuratedLists.Include(l => l.Items).ThenInclude(i => i.Book).Include(l => l.Items).ThenInclude(i => i.Author).ToListAsync();
            var recommended = lists.Single(l => l.Key == CatalogueSeeder.RecommendedKey);
            Assert.Equal(6, recommended.Items.Count);
            Assert.All(recommended.Items, item => Assert.True(item.Book!.AvailableCopies > 0));
            Assert.Equal(Enumerable.Range(0, 6), recommended.Items.OrderBy(i => i.Position).Select(i => i.Position));

            Assert.Equal(9, lists.Single(l => l.Key == CatalogueSeeder.Top100Key).Items.Count);

            var popular = lists.Single(l => l.Key == CatalogueSeeder.PopularAuthorsKey);
            Assert.Equal(CuratedListItemType.Authors, popular.ItemType);
            Assert.Equal("Busy Author", popular.Items.OrderBy(i => i.Position).First().Author!.Name);
        }

        [Fact]
        public async Task Seed_Leaves_Existing_Authors_And_Lists_Alone()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Books.Add(CatalogueTestData.Book("Emma", "Jane Austen"));
            context.Authors.Add(new Author { Name = "Hand Picked" });
            context.CuratedLists.Add(new CuratedList { Key = "staff-picks", Name = "Staff picks" });
            await context.SaveChangesAsync();

            await CatalogueSeeder.SeedAsync(context);
            await CatalogueSeeder.SeedAsync(context);

            Assert.Equal("Hand Picked", (await context.Authors.SingleAsync()).Name);
            Assert.Equal("staff-picks", (await context.CuratedLists.SingleAsync()).Key);
        }

        [Fact]
        public async Task Seed_On_An_Empty_Catalogue_Creates_Empty_Lists()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            await CatalogueSeeder.SeedAsync(context);

            Assert.Empty(await context.Authors.ToListAsync());
            Assert.Equal(3, await context.CuratedLists.CountAsync());
            Assert.Empty(await context.CuratedListItems.ToListAsync());
        }
    }
}

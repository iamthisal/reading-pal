using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InventoryService.Tests
{
    public class BookSearchControllerTests
    {
        private static async Task SeedAsync(InventoryDbContext context)
        {
            context.Books.AddRange(
                CatalogueTestData.Book("Dune", "Frank Herbert", "Science Fiction", available: 2, createdAt: new DateTime(2026, 1, 1)),
                CatalogueTestData.Book("Dune Messiah", "Frank Herbert", "Science Fiction", available: 0, createdAt: new DateTime(2026, 1, 2)),
                CatalogueTestData.Book("The Sand Dune Mystery", "Agatha Christie", "Mystery", available: 1, createdAt: new DateTime(2026, 1, 3)),
                CatalogueTestData.Book("Emma", "Jane Austen", "Classics", available: 1, createdAt: new DateTime(2026, 1, 4)),
                CatalogueTestData.Book("Persuasion", "Jane Austen", "Classics", available: 0, createdAt: new DateTime(2026, 1, 5)),
                CatalogueTestData.Book("Herbert's Garden", "Dune Gardener", "Non-fiction", available: 1, createdAt: new DateTime(2026, 1, 6)));
            await context.SaveChangesAsync();
        }

        private static async Task<BookSearchResponse> SearchAsync(InventoryDbContext context, string? q = null, string[]? genres = null,
            bool availableOnly = false, string? sort = null, int page = 1, int pageSize = BookSearchController.DefaultPageSize)
        {
            var result = await new BookSearchController(context).Search(q, genres, availableOnly, sort, page, pageSize);
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<BookSearchResponse>(ok.Value);
        }

        [Fact]
        public async Task No_Query_Returns_Every_Book_Newest_First()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context);

            Assert.Equal(6, response.Total);
            Assert.Equal("Herbert's Garden", response.Items.First().Title);
            Assert.Equal("Dune", response.Items.Last().Title);
        }

        [Theory]
        [InlineData("emma", "Emma")]
        [InlineData("AUSTEN", "Emma,Persuasion")]
        [InlineData("classics", "Emma,Persuasion")]
        [InlineData("  christie  ", "The Sand Dune Mystery")]
        public async Task Query_Matches_Title_Author_Or_Category_Ignoring_Case(string query, string expectedTitles)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, query, sort: "title");

            Assert.Equal(expectedTitles.Split(','), response.Items.Select(b => b.Title));
        }

        [Fact]
        public async Task Query_Matches_Isbn()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Clean Code");
            book.ISBN = "9780132350884";
            context.Books.AddRange(book, CatalogueTestData.Book("Other"));
            await context.SaveChangesAsync();

            var response = await SearchAsync(context, "0132350");

            Assert.Equal("Clean Code", Assert.Single(response.Items).Title);
        }

        [Fact]
        public async Task Every_Word_Must_Match_But_May_Match_Different_Fields()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, "herbert messiah");

            Assert.Equal("Dune Messiah", Assert.Single(response.Items).Title);
        }

        [Fact]
        public async Task Relevance_Puts_Title_Prefix_Then_Title_Then_Author_Matches()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, "dune");

            Assert.Equal(new[] { "Dune", "Dune Messiah", "The Sand Dune Mystery", "Herbert's Garden" }, response.Items.Select(b => b.Title));
        }

        [Theory]
        [InlineData("title", "Dune,Dune Messiah,Emma")]
        [InlineData("author", "The Sand Dune Mystery,Herbert's Garden,Dune")]
        [InlineData("newest", "Herbert's Garden,Persuasion,Emma")]
        public async Task Sort_Orders_The_Results(string sort, string expectedFirstThree)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, sort: sort);

            Assert.Equal(expectedFirstThree.Split(','), response.Items.Take(3).Select(b => b.Title));
        }

        [Fact]
        public async Task Genre_Filter_Accepts_Several_Categories()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, genres: new[] { "classics", " Mystery " }, sort: "title");

            Assert.Equal(new[] { "Emma", "Persuasion", "The Sand Dune Mystery" }, response.Items.Select(b => b.Title));
        }

        [Fact]
        public async Task AvailableOnly_Hides_Books_With_No_Copies()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, "dune", availableOnly: true);

            Assert.DoesNotContain(response.Items, b => b.Title == "Dune Messiah");
            Assert.Equal(3, response.Total);
        }

        [Fact]
        public async Task Genre_Counts_Ignore_The_Genre_Filter_But_Respect_Query_And_Availability()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, "dune", genres: new[] { "Mystery" }, availableOnly: true);

            Assert.Equal(1, response.Total);
            Assert.Equal(new[] { "Mystery:1", "Non-fiction:1", "Science Fiction:1" }, response.Facets.Genres.Select(g => $"{g.Name}:{g.Count}"));
        }

        [Fact]
        public async Task Available_Count_Ignores_The_Availability_Filter_But_Respects_Query_And_Genres()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, genres: new[] { "Classics" });

            Assert.Equal(2, response.Total);
            Assert.Equal(1, response.Facets.AvailableCount);
        }

        [Fact]
        public async Task Results_Are_Paged()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var second = await SearchAsync(context, sort: "title", page: 2, pageSize: 4);

            Assert.Equal(6, second.Total);
            Assert.Equal(2, second.TotalPages);
            Assert.Equal(2, second.Page);
            Assert.Equal(new[] { "Persuasion", "The Sand Dune Mystery" }, second.Items.Select(b => b.Title));
        }

        [Theory]
        [InlineData(99, 2)]
        [InlineData(0, 1)]
        [InlineData(-3, 1)]
        public async Task Out_Of_Range_Pages_Are_Clamped(int requested, int expected)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, page: requested, pageSize: 4);

            Assert.Equal(expected, response.Page);
            Assert.NotEmpty(response.Items);
        }

        [Theory]
        [InlineData(0, BookSearchController.DefaultPageSize)]
        [InlineData(1000, BookSearchController.MaxPageSize)]
        public async Task Page_Size_Is_Kept_Within_Limits(int requested, int expected)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            Assert.Equal(expected, (await SearchAsync(context, pageSize: requested)).PageSize);
        }

        [Fact]
        public async Task No_Matches_Returns_An_Empty_First_Page()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            var response = await SearchAsync(context, "zzzz");

            Assert.Empty(response.Items);
            Assert.Equal(0, response.Total);
            Assert.Equal(1, response.Page);
            Assert.Equal(1, response.TotalPages);
            Assert.Empty(response.Facets.Genres);
        }

        [Fact]
        public async Task Wildcard_Characters_Are_Treated_As_Plain_Text()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await SeedAsync(context);

            Assert.Equal(0, (await SearchAsync(context, "%")).Total);
            Assert.Equal(0, (await SearchAsync(context, "_une")).Total);
        }

        [Fact]
        public void Search_Is_Public()
        {
            Assert.Null(typeof(BookSearchController).GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(typeof(BookSearchController).GetMethod(nameof(BookSearchController.Search))!.GetCustomAttribute<AuthorizeAttribute>());
        }
    }
}

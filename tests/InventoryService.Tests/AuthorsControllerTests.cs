using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.DTOs;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class AuthorsControllerTests
    {
        [Fact]
        public async Task GetAll_Returns_Authors_By_Name_With_Book_Counts()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Authors.AddRange(new Author { Name = "Zadie Smith" }, new Author { Name = "Jane Austen" });
            context.Books.AddRange(
                CatalogueTestData.Book("Emma", "Jane Austen"),
                CatalogueTestData.Book("Persuasion", " jane austen "),
                CatalogueTestData.Book("Other", "Someone Else"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetAll();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var authors = Assert.IsAssignableFrom<IEnumerable<AuthorResponse>>(ok.Value).ToList();
            Assert.Equal(new[] { "Jane Austen", "Zadie Smith" }, authors.Select(a => a.Name));
            Assert.Equal(2, authors[0].BookCount);
            Assert.Equal(0, authors[1].BookCount);
        }

        [Fact]
        public async Task GetById_Unknown_Author_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new AuthorsController(context).GetById(99);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task GetBooks_Returns_Only_That_Authors_Books_Newest_First()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen" };
            context.Authors.Add(author);
            context.Books.AddRange(
                CatalogueTestData.Book("Older", "Jane Austen", createdAt: new System.DateTime(2026, 1, 1)),
                CatalogueTestData.Book("Newer", "JANE AUSTEN", createdAt: new System.DateTime(2026, 2, 1)),
                CatalogueTestData.Book("Other", "Someone Else"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetBooks(author.Id);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var books = Assert.IsAssignableFrom<IEnumerable<BookResponse>>(ok.Value).ToList();
            Assert.Equal(new[] { "Newer", "Older" }, books.Select(b => b.Title));
        }

        [Fact]
        public async Task GetBooks_Respects_Limit()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen" };
            context.Authors.Add(author);
            context.Books.AddRange(Enumerable.Range(1, 4).Select(n => CatalogueTestData.Book($"Book {n}", "Jane Austen")));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetBooks(author.Id, limit: 2);

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(2, Assert.IsAssignableFrom<IEnumerable<BookResponse>>(ok.Value).Count());
        }

        [Fact]
        public async Task GetBooks_Unknown_Author_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new AuthorsController(context).GetBooks(99);

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task Admin_Can_Create_Author_With_Trimmed_Name()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new AuthorsController(context).Create(new CreateAuthorRequest { Name = "  Jane Austen  " });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<AuthorResponse>(created.Value);
            Assert.Equal("Jane Austen", response.Name);
            Assert.NotEqual(0, response.Id);
        }

        [Fact]
        public async Task Create_Duplicate_Author_Is_Rejected_Case_Insensitively()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Authors.Add(new Author { Name = "Jane Austen" });
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).Create(new CreateAuthorRequest { Name = " jane austen " });

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task Create_With_Invalid_Model_Returns_BadRequest()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var controller = new AuthorsController(context);
            controller.ModelState.AddModelError("Name", "Author name is required");

            var result = await controller.Create(new CreateAuthorRequest());

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_Renames_The_Authors_Books()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "J K Rowling" };
            context.Authors.Add(author);
            context.Books.AddRange(CatalogueTestData.Book("One", "J K Rowling"), CatalogueTestData.Book("Two", "Someone Else"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).Update(author.Id, new UpdateAuthorRequest { Name = "J.K. Rowling" });

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(1, Assert.IsType<AuthorResponse>(ok.Value).BookCount);
            Assert.Equal("J.K. Rowling", (await context.Books.SingleAsync(b => b.Title == "One")).Author);
            Assert.Equal("Someone Else", (await context.Books.SingleAsync(b => b.Title == "Two")).Author);
        }

        [Fact]
        public async Task Update_To_Another_Authors_Name_Returns_Conflict()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var first = new Author { Name = "First" };
            context.Authors.AddRange(first, new Author { Name = "Second" });
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).Update(first.Id, new UpdateAuthorRequest { Name = "second" });

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Fact]
        public async Task Update_Unknown_Author_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new AuthorsController(context).Update(99, new UpdateAuthorRequest { Name = "Nobody" });

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task Delete_Removes_The_Author_But_Keeps_Their_Books()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen" };
            context.Authors.Add(author);
            context.Books.Add(CatalogueTestData.Book("Emma", "Jane Austen"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).Delete(author.Id);

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(await context.Authors.ToListAsync());
            Assert.Single(await context.Books.ToListAsync());
        }

        [Fact]
        public async Task Delete_Unknown_Author_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            Assert.IsType<NotFoundObjectResult>(await new AuthorsController(context).Delete(99));
        }

        [Theory]
        [InlineData(nameof(AuthorsController.Create))]
        [InlineData(nameof(AuthorsController.Update))]
        [InlineData(nameof(AuthorsController.Delete))]
        public void Write_Endpoints_Require_Admin(string method)
        {
            var authorize = typeof(AuthorsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal("Admin", authorize!.Roles);
        }

        [Theory]
        [InlineData(nameof(AuthorsController.GetAll))]
        [InlineData(nameof(AuthorsController.GetById))]
        [InlineData(nameof(AuthorsController.GetBooks))]
        public void Read_Endpoints_Are_Public(string method)
        {
            Assert.Null(typeof(AuthorsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(typeof(AuthorsController).GetCustomAttribute<AuthorizeAttribute>());
        }
    }
}

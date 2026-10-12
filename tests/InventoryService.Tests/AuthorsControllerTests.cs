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
        public async Task GetProfile_Returns_The_Author_And_Their_Books_Newest_First()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen" };
            context.Authors.Add(author);
            context.Books.AddRange(
                CatalogueTestData.Book("Older", "Jane Austen", createdAt: new System.DateTime(2026, 1, 1)),
                CatalogueTestData.Book("Newer", "jane austen", createdAt: new System.DateTime(2026, 2, 1)),
                CatalogueTestData.Book("Other", "Someone Else"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetProfile("  JANE AUSTEN ");

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var profile = Assert.IsType<AuthorProfileResponse>(ok.Value);
            Assert.Equal(author.Id, profile.Id);
            Assert.Equal("Jane Austen", profile.Name);
            Assert.Equal(2, profile.BookCount);
            Assert.Equal(new[] { "Newer", "Older" }, profile.Books.Select(b => b.Title));
        }

        [Fact]
        public async Task GetProfile_Works_For_An_Author_With_Books_But_No_Author_Entry()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Books.Add(CatalogueTestData.Book("Emma", "Jane Austen"));
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetProfile("jane austen");

            var profile = Assert.IsType<AuthorProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Null(profile.Id);
            Assert.Equal("Jane Austen", profile.Name);
            Assert.Equal("Emma", Assert.Single(profile.Books).Title);
        }

        [Fact]
        public async Task GetProfile_For_An_Author_Entry_Without_Books_Returns_An_Empty_List()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Authors.Add(new Author { Name = "New Writer" });
            await context.SaveChangesAsync();

            var result = await new AuthorsController(context).GetProfile("New Writer");

            var profile = Assert.IsType<AuthorProfileResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);
            Assert.Equal(0, profile.BookCount);
            Assert.Empty(profile.Books);
        }

        [Fact]
        public async Task GetProfile_Unknown_Name_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            Assert.IsType<NotFoundObjectResult>((await new AuthorsController(context).GetProfile("Nobody")).Result);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task GetProfile_Without_A_Name_Returns_BadRequest(string? name)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            Assert.IsType<BadRequestObjectResult>((await new AuthorsController(context).GetProfile(name)).Result);
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
        public async Task Create_Stores_Description_And_Image()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new AuthorsController(context).Create(new CreateAuthorRequest
            {
                Name = "Jane Austen", Description = "  English novelist.  ", ImageUrl = " https://example.com/austen.jpg "
            });

            var response = Assert.IsType<AuthorResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
            Assert.Equal("English novelist.", response.Description);
            Assert.Equal("https://example.com/austen.jpg", response.ImageUrl);
        }

        [Fact]
        public async Task Update_Keeps_Omitted_Details_And_Clears_Empty_Ones()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen", Description = "English novelist.", ImageUrl = "https://example.com/austen.jpg" };
            context.Authors.Add(author);
            await context.SaveChangesAsync();
            var controller = new AuthorsController(context);

            await controller.Update(author.Id, new UpdateAuthorRequest { Name = "Jane Austen" });
            Assert.Equal("English novelist.", author.Description);
            Assert.Equal("https://example.com/austen.jpg", author.ImageUrl);

            await controller.Update(author.Id, new UpdateAuthorRequest { Name = "Jane Austen", Description = "New text.", ImageUrl = "  " });
            Assert.Equal("New text.", author.Description);
            Assert.Null(author.ImageUrl);
        }

        [Theory]
        [InlineData(null, true)]
        [InlineData("", true)]
        [InlineData("   ", true)]
        [InlineData("https://example.com/a.jpg", true)]
        [InlineData("not a link", false)]
        [InlineData("ftp://example.com/a.jpg", false)]
        public void Update_Request_Accepts_An_Empty_Image_Url_But_Not_A_Malformed_One(string? imageUrl, bool expectedValid)
        {
            var request = new UpdateAuthorRequest { Name = "Jane Austen", ImageUrl = imageUrl };

            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                request, new System.ComponentModel.DataAnnotations.ValidationContext(request), null, validateAllProperties: true);

            Assert.Equal(expectedValid, valid);
        }

        [Fact]
        public async Task GetProfile_Includes_Description_And_Image()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            context.Authors.Add(new Author { Name = "Jane Austen", Description = "English novelist.", ImageUrl = "https://example.com/austen.jpg" });
            await context.SaveChangesAsync();

            var profile = Assert.IsType<AuthorProfileResponse>(Assert.IsType<OkObjectResult>((await new AuthorsController(context).GetProfile("jane austen")).Result).Value);

            Assert.Equal("English novelist.", profile.Description);
            Assert.Equal("https://example.com/austen.jpg", profile.ImageUrl);
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
        [InlineData(nameof(AuthorsController.GetProfile))]
        public void Read_Endpoints_Are_Public(string method)
        {
            Assert.Null(typeof(AuthorsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(typeof(AuthorsController).GetCustomAttribute<AuthorizeAttribute>());
        }
    }
}

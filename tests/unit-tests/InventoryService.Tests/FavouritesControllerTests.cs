using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class FavouritesControllerTests
    {
        private static FavouritesController ControllerFor(InventoryDbContext context, string? userId)
        {
            var identity = userId == null
                ? new ClaimsIdentity()
                : new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, userId) }, "Test");
            return new FavouritesController(context)
            {
                ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) } }
            };
        }

        private static List<BookResponse> Books(ActionResult<IEnumerable<BookResponse>> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsAssignableFrom<IEnumerable<BookResponse>>(ok.Value).ToList();
        }

        [Fact]
        public void FavouriteBook_DefaultValues_AreProperlyInitialized()
        {
            var favourite = new FavouriteBook();

            Assert.Equal(string.Empty, favourite.UserId);
            Assert.Equal(0, favourite.BookId);
            Assert.True(favourite.CreatedAt <= System.DateTime.UtcNow);
        }

        [Fact]
        public async Task Add_Marks_The_Book_For_That_Reader_Only()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var result = await ControllerFor(context, "reader-1").Add(book.Id);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("Emma", Assert.Single(Books(await ControllerFor(context, "reader-1").GetAll())).Title);
            Assert.Empty(Books(await ControllerFor(context, "reader-2").GetAll()));
        }

        [Fact]
        public async Task Add_Twice_Keeps_A_Single_Favourite()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            await context.SaveChangesAsync();
            var controller = ControllerFor(context, "reader-1");

            await controller.Add(book.Id);
            var second = await controller.Add(book.Id);

            Assert.IsType<NoContentResult>(second);
            Assert.Equal(1, await context.FavouriteBooks.CountAsync());
        }

        [Fact]
        public async Task Add_Unknown_Book_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await ControllerFor(context, "reader-1").Add(999);

            Assert.IsType<NotFoundObjectResult>(result);
            Assert.Empty(await context.FavouriteBooks.ToListAsync());
        }

        [Fact]
        public async Task GetAll_Returns_Most_Recently_Added_First()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var first = CatalogueTestData.Book("First");
            var second = CatalogueTestData.Book("Second");
            context.Books.AddRange(first, second);
            await context.SaveChangesAsync();
            context.FavouriteBooks.AddRange(
                new FavouriteBook { UserId = "reader-1", BookId = first.Id, CreatedAt = new System.DateTime(2026, 1, 1) },
                new FavouriteBook { UserId = "reader-1", BookId = second.Id, CreatedAt = new System.DateTime(2026, 2, 1) });
            await context.SaveChangesAsync();

            var books = Books(await ControllerFor(context, "reader-1").GetAll());

            Assert.Equal(new[] { "Second", "First" }, books.Select(b => b.Title));
        }

        [Fact]
        public async Task GetIds_Returns_Only_The_Readers_Own_Book_Ids()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var mine = CatalogueTestData.Book("Mine");
            var theirs = CatalogueTestData.Book("Theirs");
            context.Books.AddRange(mine, theirs);
            await context.SaveChangesAsync();
            context.FavouriteBooks.AddRange(
                new FavouriteBook { UserId = "reader-1", BookId = mine.Id },
                new FavouriteBook { UserId = "reader-2", BookId = theirs.Id });
            await context.SaveChangesAsync();

            var result = await ControllerFor(context, "reader-1").GetIds();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal(new[] { mine.Id }, Assert.IsAssignableFrom<IEnumerable<int>>(ok.Value));
        }

        [Fact]
        public async Task Remove_Takes_The_Book_Off_Only_That_Readers_Favourites()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            await context.SaveChangesAsync();
            context.FavouriteBooks.AddRange(
                new FavouriteBook { UserId = "reader-1", BookId = book.Id },
                new FavouriteBook { UserId = "reader-2", BookId = book.Id });
            await context.SaveChangesAsync();

            var result = await ControllerFor(context, "reader-1").Remove(book.Id);

            Assert.IsType<NoContentResult>(result);
            Assert.Equal("reader-2", (await context.FavouriteBooks.SingleAsync()).UserId);
        }

        [Fact]
        public async Task Remove_A_Book_That_Is_Not_A_Favourite_Is_Harmless()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            Assert.IsType<NoContentResult>(await ControllerFor(context, "reader-1").Remove(123));
        }

        [Fact]
        public async Task Deleting_A_Book_Removes_It_From_Favourites()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            await context.SaveChangesAsync();
            context.FavouriteBooks.Add(new FavouriteBook { UserId = "reader-1", BookId = book.Id });
            await context.SaveChangesAsync();

            context.Books.Remove(book);
            await context.SaveChangesAsync();

            using var check = factory.CreateContext();
            Assert.Empty(await check.FavouriteBooks.ToListAsync());
        }

        [Fact]
        public async Task The_Same_Reader_Cannot_Hold_The_Same_Favourite_Twice_In_The_Database()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            await context.SaveChangesAsync();
            context.FavouriteBooks.AddRange(
                new FavouriteBook { UserId = "reader-1", BookId = book.Id },
                new FavouriteBook { UserId = "reader-1", BookId = book.Id });

            await Assert.ThrowsAsync<DbUpdateException>(() => context.SaveChangesAsync());
        }

        [Fact]
        public async Task Requests_Without_A_User_Id_Are_Unauthorized()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var controller = ControllerFor(context, null);

            Assert.IsType<UnauthorizedResult>((await controller.GetAll()).Result);
            Assert.IsType<UnauthorizedResult>((await controller.GetIds()).Result);
            Assert.IsType<UnauthorizedResult>(await controller.Add(1));
            Assert.IsType<UnauthorizedResult>(await controller.Remove(1));
        }

        [Fact]
        public void Controller_Requires_A_Signed_In_User_Of_Any_Role()
        {
            var authorize = typeof(FavouritesController).GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Null(authorize!.Roles);
        }
    }
}

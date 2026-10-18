using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class ListsControllerTests
    {
        private static async Task<CuratedList> AddListAsync(InventoryDbContext context, string key, string name, CuratedListItemType type = CuratedListItemType.Books)
        {
            var list = new CuratedList { Key = key, Name = name, ItemType = type };
            context.CuratedLists.Add(list);
            await context.SaveChangesAsync();
            return list;
        }

        private static CuratedListResponse Body(ActionResult<CuratedListResponse> result)
        {
            var ok = Assert.IsType<OkObjectResult>(result.Result);
            return Assert.IsType<CuratedListResponse>(ok.Value);
        }

        [Fact]
        public async Task GetAll_Returns_Lists_By_Name_With_Item_Counts()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("Emma");
            context.Books.Add(book);
            var top = await AddListAsync(context, "top-100", "Top 100");
            await AddListAsync(context, "popular-authors", "Popular Authors", CuratedListItemType.Authors);
            context.CuratedListItems.Add(new CuratedListItem { CuratedListId = top.Id, BookId = book.Id });
            await context.SaveChangesAsync();

            var result = await new ListsController(context).GetAll();

            var ok = Assert.IsType<OkObjectResult>(result.Result);
            var lists = Assert.IsAssignableFrom<IEnumerable<CuratedListSummaryResponse>>(ok.Value).ToList();
            Assert.Equal(new[] { "popular-authors", "top-100" }, lists.Select(l => l.Key));
            Assert.Equal("Authors", lists[0].ItemType);
            Assert.Equal(0, lists[0].ItemCount);
            Assert.Equal(1, lists[1].ItemCount);
        }

        [Fact]
        public async Task GetByKey_Returns_Books_In_List_Order()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var first = CatalogueTestData.Book("First");
            var second = CatalogueTestData.Book("Second", available: 0);
            context.Books.AddRange(first, second);
            var list = await AddListAsync(context, "top-100", "Top 100");
            context.CuratedListItems.AddRange(
                new CuratedListItem { CuratedListId = list.Id, BookId = second.Id, Position = 1 },
                new CuratedListItem { CuratedListId = list.Id, BookId = first.Id, Position = 0 });
            await context.SaveChangesAsync();

            var body = Body(await new ListsController(context).GetByKey("TOP-100"));

            Assert.Equal("Books", body.ItemType);
            Assert.Equal(2, body.ItemCount);
            Assert.Equal(new[] { "First", "Second" }, body.Books.Select(b => b.Title));
            Assert.False(body.Books[1].IsAvailable);
            Assert.Empty(body.Authors);
        }

        [Fact]
        public async Task GetByKey_With_Limit_Returns_The_First_Items_And_The_Full_Count()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var books = Enumerable.Range(0, 5).Select(n => CatalogueTestData.Book($"Book {n}")).ToList();
            context.Books.AddRange(books);
            var list = await AddListAsync(context, "top-100", "Top 100");
            context.CuratedListItems.AddRange(books.Select((book, position) => new CuratedListItem { CuratedListId = list.Id, BookId = book.Id, Position = position }));
            await context.SaveChangesAsync();

            var body = Body(await new ListsController(context).GetByKey("top-100", limit: 2));

            Assert.Equal(new[] { "Book 0", "Book 1" }, body.Books.Select(b => b.Title));
            Assert.Equal(5, body.ItemCount);
        }

        [Fact]
        public async Task GetByKey_Returns_Authors_With_Book_Counts_For_An_Author_List()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var austen = new Author { Name = "Jane Austen" };
            var smith = new Author { Name = "Zadie Smith" };
            context.Authors.AddRange(austen, smith);
            context.Books.Add(CatalogueTestData.Book("Emma", "Jane Austen"));
            var list = await AddListAsync(context, "popular-authors", "Popular Authors", CuratedListItemType.Authors);
            context.CuratedListItems.AddRange(
                new CuratedListItem { CuratedListId = list.Id, AuthorId = smith.Id, Position = 0 },
                new CuratedListItem { CuratedListId = list.Id, AuthorId = austen.Id, Position = 1 });
            await context.SaveChangesAsync();

            var body = Body(await new ListsController(context).GetByKey("popular-authors"));

            Assert.Equal(new[] { "Zadie Smith", "Jane Austen" }, body.Authors.Select(a => a.Name));
            Assert.Equal(1, body.Authors[1].BookCount);
            Assert.Empty(body.Books);
        }

        [Fact]
        public async Task GetByKey_Unknown_List_Returns_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new ListsController(context).GetByKey("nope");

            Assert.IsType<NotFoundObjectResult>(result.Result);
        }

        [Fact]
        public async Task Admin_Can_Create_List()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new ListsController(context).Create(new CreateCuratedListRequest { Key = "top-100", Name = "  Top 100 ", ItemType = "books" });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var body = Assert.IsType<CuratedListResponse>(created.Value);
            Assert.Equal("top-100", body.Key);
            Assert.Equal("Top 100", body.Name);
            Assert.Equal("Books", body.ItemType);
            Assert.Equal(0, body.ItemCount);
        }

        [Fact]
        public async Task Create_With_Existing_Key_Returns_Conflict()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await AddListAsync(context, "top-100", "Top 100");

            var result = await new ListsController(context).Create(new CreateCuratedListRequest { Key = "top-100", Name = "Another", ItemType = "Books" });

            Assert.IsType<ConflictObjectResult>(result.Result);
        }

        [Theory]
        [InlineData("Magazines")]
        [InlineData("7")]
        public async Task Create_With_Unknown_Item_Type_Returns_BadRequest(string itemType)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var result = await new ListsController(context).Create(new CreateCuratedListRequest { Key = "mags", Name = "Mags", ItemType = itemType });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Theory]
        [InlineData("top-100", true)]
        [InlineData("popular", true)]
        [InlineData("Top 100", false)]
        [InlineData("top--100", false)]
        [InlineData("-top", false)]
        [InlineData("", false)]
        public void Create_Request_Validates_The_Key_Format(string key, bool expectedValid)
        {
            var request = new CreateCuratedListRequest { Key = key, Name = "Name", ItemType = "Books" };

            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                request, new System.ComponentModel.DataAnnotations.ValidationContext(request), null, validateAllProperties: true);

            Assert.Equal(expectedValid, valid);
        }

        [Fact]
        public async Task Update_Renames_The_List_And_Keeps_Its_Key()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await AddListAsync(context, "top-100", "Top 100");

            var body = Body(await new ListsController(context).Update("top-100", new UpdateCuratedListRequest { Name = " Readers' favourites " }));

            Assert.Equal("Readers' favourites", body.Name);
            Assert.Equal("top-100", body.Key);
        }

        [Fact]
        public async Task SetItems_Replaces_The_Contents_In_The_Given_Order()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var a = CatalogueTestData.Book("A");
            var b = CatalogueTestData.Book("B");
            var c = CatalogueTestData.Book("C");
            context.Books.AddRange(a, b, c);
            var list = await AddListAsync(context, "top-100", "Top 100");
            context.CuratedListItems.AddRange(
                new CuratedListItem { CuratedListId = list.Id, BookId = a.Id, Position = 0 },
                new CuratedListItem { CuratedListId = list.Id, BookId = b.Id, Position = 1 });
            await context.SaveChangesAsync();
            var controller = new ListsController(context);

            var body = Body(await controller.SetItems("top-100", new SetCuratedListItemsRequest { ItemIds = new List<int> { c.Id, a.Id } }));

            Assert.Equal(new[] { "C", "A" }, body.Books.Select(book => book.Title));
            Assert.Equal(2, await context.CuratedListItems.CountAsync());
        }

        [Fact]
        public async Task SetItems_With_An_Empty_List_Clears_It()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("A");
            context.Books.Add(book);
            var list = await AddListAsync(context, "top-100", "Top 100");
            context.CuratedListItems.Add(new CuratedListItem { CuratedListId = list.Id, BookId = book.Id });
            await context.SaveChangesAsync();

            var body = Body(await new ListsController(context).SetItems("top-100", new SetCuratedListItemsRequest()));

            Assert.Empty(body.Books);
            Assert.Equal(0, body.ItemCount);
        }

        [Fact]
        public async Task SetItems_On_An_Author_List_Uses_Author_Ids()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var author = new Author { Name = "Jane Austen" };
            context.Authors.Add(author);
            await AddListAsync(context, "popular-authors", "Popular Authors", CuratedListItemType.Authors);

            var body = Body(await new ListsController(context).SetItems("popular-authors", new SetCuratedListItemsRequest { ItemIds = new List<int> { author.Id } }));

            Assert.Equal("Jane Austen", Assert.Single(body.Authors).Name);
            var item = await context.CuratedListItems.SingleAsync();
            Assert.Equal(author.Id, item.AuthorId);
            Assert.Null(item.BookId);
        }

        [Fact]
        public async Task SetItems_With_Unknown_Id_Returns_BadRequest_And_Changes_Nothing()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("A");
            context.Books.Add(book);
            var list = await AddListAsync(context, "top-100", "Top 100");
            context.CuratedListItems.Add(new CuratedListItem { CuratedListId = list.Id, BookId = book.Id });
            await context.SaveChangesAsync();

            var result = await new ListsController(context).SetItems("top-100", new SetCuratedListItemsRequest { ItemIds = new List<int> { book.Id, 9999 } });

            Assert.IsType<BadRequestObjectResult>(result.Result);
            Assert.Equal(1, await context.CuratedListItems.CountAsync());
        }

        [Fact]
        public async Task SetItems_With_Duplicate_Ids_Returns_BadRequest()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var book = CatalogueTestData.Book("A");
            context.Books.Add(book);
            await AddListAsync(context, "top-100", "Top 100");

            var result = await new ListsController(context).SetItems("top-100", new SetCuratedListItemsRequest { ItemIds = new List<int> { book.Id, book.Id } });

            Assert.IsType<BadRequestObjectResult>(result.Result);
        }

        [Fact]
        public async Task Delete_Removes_The_List()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            await AddListAsync(context, "top-100", "Top 100");

            var result = await new ListsController(context).Delete("top-100");

            Assert.IsType<NoContentResult>(result);
            Assert.Empty(await context.CuratedLists.ToListAsync());
        }

        [Fact]
        public async Task Write_Endpoints_On_Unknown_List_Return_NotFound()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var controller = new ListsController(context);

            Assert.IsType<NotFoundObjectResult>((await controller.Update("nope", new UpdateCuratedListRequest { Name = "X" })).Result);
            Assert.IsType<NotFoundObjectResult>((await controller.SetItems("nope", new SetCuratedListItemsRequest())).Result);
            Assert.IsType<NotFoundObjectResult>(await controller.Delete("nope"));
        }

        [Theory]
        [InlineData(nameof(ListsController.Create))]
        [InlineData(nameof(ListsController.Update))]
        [InlineData(nameof(ListsController.SetItems))]
        [InlineData(nameof(ListsController.Delete))]
        public void Write_Endpoints_Require_Admin(string method)
        {
            var authorize = typeof(ListsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>();

            Assert.NotNull(authorize);
            Assert.Equal("Admin", authorize!.Roles);
        }

        [Theory]
        [InlineData(nameof(ListsController.GetAll))]
        [InlineData(nameof(ListsController.GetByKey))]
        public void Read_Endpoints_Are_Public(string method)
        {
            Assert.Null(typeof(ListsController).GetMethod(method)!.GetCustomAttribute<AuthorizeAttribute>());
            Assert.Null(typeof(ListsController).GetCustomAttribute<AuthorizeAttribute>());
        }
    }
}

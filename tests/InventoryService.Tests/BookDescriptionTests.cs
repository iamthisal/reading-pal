using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.DTOs;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace InventoryService.Tests
{
    public class BookDescriptionTests
    {
        private static CreateBookRequest NewBook(string? description) => new()
        {
            Title = "Emma", Author = "Jane Austen", ISBN = "9780141439587", Genre = "Classics", TotalCopies = 2, Description = description
        };

        private static UpdateBookRequest Changed(string? description) => new()
        {
            Title = "Emma", Author = "Jane Austen", ISBN = "9780141439587", Genre = "Classics", TotalCopies = 2, Description = description
        };

        [Fact]
        public async Task Create_Stores_A_Trimmed_Description_And_Returns_It()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var controller = new BooksController(context);

            var created = Assert.IsType<CreatedAtActionResult>((await controller.Create(NewBook("  A matchmaker meets her match.  "))).Result);
            var book = Assert.IsType<BookResponse>(created.Value);

            Assert.Equal("A matchmaker meets her match.", book.Description);
            var fetched = Assert.IsType<BookResponse>(Assert.IsType<OkObjectResult>((await controller.GetById(book.Id)).Result).Value);
            Assert.Equal("A matchmaker meets her match.", fetched.Description);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("   ")]
        public async Task Create_Without_A_Description_Stores_None(string? description)
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();

            var created = Assert.IsType<CreatedAtActionResult>((await new BooksController(context).Create(NewBook(description))).Result);

            Assert.Null(Assert.IsType<BookResponse>(created.Value).Description);
        }

        [Fact]
        public async Task Update_Replaces_Keeps_Or_Clears_The_Description()
        {
            using var factory = new SqliteInventoryDbContextFactory();
            using var context = factory.CreateContext();
            var controller = new BooksController(context);
            var id = Assert.IsType<BookResponse>(Assert.IsType<CreatedAtActionResult>((await controller.Create(NewBook("First."))).Result).Value).Id;

            BookResponse After(ActionResult<BookResponse> result) => Assert.IsType<BookResponse>(Assert.IsType<OkObjectResult>(result.Result).Value);

            Assert.Equal("Second.", After(await controller.Update(id, Changed("Second."))).Description);
            // Older clients do not send the field at all: the description must survive.
            Assert.Equal("Second.", After(await controller.Update(id, Changed(null))).Description);
            Assert.Null(After(await controller.Update(id, Changed(""))).Description);
        }

        [Fact]
        public void Description_Longer_Than_The_Limit_Is_Invalid()
        {
            var request = NewBook(new string('x', 2001));

            var valid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                request, new System.ComponentModel.DataAnnotations.ValidationContext(request), null, validateAllProperties: true);

            Assert.False(valid);
        }
    }
}

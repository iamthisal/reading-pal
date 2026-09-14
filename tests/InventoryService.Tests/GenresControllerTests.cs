using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace InventoryService.Tests
{
    public class GenresControllerTests
    {
        private InventoryDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new InventoryDbContext(options);
        }

        [Fact]
        public async Task Admin_Can_Create_Genre()
        {
            using var context = GetDbContext();
            var controller = new GenresController(context);

            var result = await controller.Create(new CreateGenreRequest { Name = "  Science Fiction  " });

            var created = Assert.IsType<CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<GenreResponse>(created.Value);
            Assert.Equal("Science Fiction", response.Name);
            Assert.NotEqual(0, response.Id);
        }

        [Fact]
        public async Task Create_Duplicate_Genre_Is_Rejected_Case_Insensitively()
        {
            using var context = GetDbContext();
            context.Genres.Add(new Genre { Name = "Fantasy" });
            await context.SaveChangesAsync();
            var controller = new GenresController(context);

            var result = await controller.Create(new CreateGenreRequest { Name = " fantasy " });

            var conflict = Assert.IsType<ConflictObjectResult>(result.Result);
            Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public async Task Update_Genre_Renames_Assigned_Books()
        {
            using var context = GetDbContext();
            var genre = new Genre { Name = "History" };
            context.Genres.Add(genre);
            context.Books.Add(new Book
            {
                Title = "A History Book",
                Author = "Author",
                ISBN = "history-1",
                Genre = "History",
                TotalCopies = 1,
                AvailableCopies = 1
            });
            await context.SaveChangesAsync();
            var controller = new GenresController(context);

            var result = await controller.Update(genre.Id, new UpdateGenreRequest { Name = "World History" });

            Assert.IsType<OkObjectResult>(result.Result);
            Assert.Equal("World History", (await context.Books.SingleAsync()).Genre);
        }

        [Fact]
        public async Task Delete_In_Use_Genre_Returns_Conflict()
        {
            using var context = GetDbContext();
            var genre = new Genre { Name = "Biography" };
            context.Genres.Add(genre);
            context.Books.Add(new Book
            {
                Title = "A Life",
                Author = "Author",
                ISBN = "bio-1",
                Genre = "Biography",
                TotalCopies = 1,
                AvailableCopies = 1
            });
            await context.SaveChangesAsync();
            var controller = new GenresController(context);

            var result = await controller.Delete(genre.Id);

            var conflict = Assert.IsType<ConflictObjectResult>(result);
            Assert.Equal(409, conflict.StatusCode);
        }

        [Fact]
        public void Mutating_Genre_Endpoints_Require_Admin_Role()
        {
            var methods = new[]
            {
                nameof(GenresController.Create),
                nameof(GenresController.Update),
                nameof(GenresController.Delete)
            };

            foreach (var method in methods)
            {
                var methodInfo = typeof(GenresController).GetMethod(method);
                Assert.NotNull(methodInfo);
                var authorize = methodInfo!.GetCustomAttribute<AuthorizeAttribute>();
                Assert.NotNull(authorize);
                Assert.Equal("Admin", authorize!.Roles);
            }
        }
    }
}

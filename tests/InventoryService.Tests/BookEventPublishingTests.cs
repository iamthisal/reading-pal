using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace InventoryService.Tests
{
    public class BookEventPublishingTests
    {
        [Fact]
        public async Task Create_Publishes_BookCreated_Event_After_Save()
        {
            using var context = CreateDbContext();
            var publisher = new RecordingBookEventPublisher();
            var controller = new BooksController(context, publisher, NullLogger<BooksController>.Instance);

            var result = await controller.Create(new CreateBookRequest
            {
                Title = "Event Driven Design",
                Author = "A. Developer",
                ISBN = "EVENT-001",
                Genre = "Technology",
                TotalCopies = 3
            });

            var created = Assert.IsType<Microsoft.AspNetCore.Mvc.CreatedAtActionResult>(result.Result);
            var response = Assert.IsType<BookResponse>(created.Value);
            var bookEvent = Assert.Single(publisher.Events);

            Assert.Equal("book-created", bookEvent.EventType);
            Assert.Equal(response.Id, bookEvent.BookId);
            Assert.Equal(response.Id, bookEvent.Book.Id);
            Assert.Equal(3, bookEvent.Book.AvailableCopies);
            Assert.NotEqual(Guid.Empty, bookEvent.EventId);
        }

        [Fact]
        public async Task MarkUnavailable_Publishes_BookUpdated_Event()
        {
            using var context = CreateDbContext();
            var book = new Models.Book
            {
                Title = "Availability Event",
                Author = "A. Developer",
                ISBN = "EVENT-002",
                Genre = "Technology",
                TotalCopies = 2,
                AvailableCopies = 2,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();

            var publisher = new RecordingBookEventPublisher();
            var controller = new BooksController(context, publisher, NullLogger<BooksController>.Instance);

            await controller.MarkUnavailable(book.Id);

            var bookEvent = Assert.Single(publisher.Events);
            Assert.Equal("book-updated", bookEvent.EventType);
            Assert.Equal(book.Id, bookEvent.BookId);
            Assert.Equal(0, bookEvent.Book.AvailableCopies);
        }

        [Fact]
        public async Task Create_RecordsTheActingAdminAndAction()
        {
            using var context = CreateDbContext();
            var publisher = new RecordingBookEventPublisher();
            var controller = new BooksController(context, publisher, NullLogger<BooksController>.Instance);
            AttachAdmin(controller, "12", "admin2@library.test");

            await controller.Create(new CreateBookRequest
            {
                Title = "Who Added This", Author = "A. Developer", ISBN = "EVENT-003", Genre = "Technology", TotalCopies = 1
            });

            var bookEvent = Assert.Single(publisher.Events);
            Assert.Equal("created", bookEvent.Action);
            Assert.Equal("12", bookEvent.PerformedBy);
            Assert.Equal("admin2@library.test", bookEvent.PerformedByEmail);
        }

        [Theory]
        [InlineData("update", "updated")]
        [InlineData("unavailable", "marked-unavailable")]
        [InlineData("available", "marked-available")]
        public async Task BookUpdatedEvents_CarryTheSpecificAction(string operation, string expectedAction)
        {
            using var context = CreateDbContext();
            var book = new Models.Book
            {
                Title = "Action Event", Author = "A. Developer", ISBN = "EVENT-004", Genre = "Technology",
                TotalCopies = 2, AvailableCopies = operation == "available" ? 0 : 2,
                CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            context.Books.Add(book);
            await context.SaveChangesAsync();
            var publisher = new RecordingBookEventPublisher();
            var controller = new BooksController(context, publisher, NullLogger<BooksController>.Instance);
            AttachAdmin(controller, "admin-id", "admin@library.com");

            switch (operation)
            {
                case "update":
                    await controller.Update(book.Id, new UpdateBookRequest
                    {
                        Title = "Action Event (2nd ed.)", Author = "A. Developer", ISBN = "EVENT-004", Genre = "Technology", TotalCopies = 2
                    });
                    break;
                case "unavailable": await controller.MarkUnavailable(book.Id); break;
                default: await controller.MarkAvailable(book.Id); break;
            }

            var bookEvent = Assert.Single(publisher.Events);
            Assert.Equal("book-updated", bookEvent.EventType);
            Assert.Equal(expectedAction, bookEvent.Action);
            Assert.Equal("admin-id", bookEvent.PerformedBy);
        }

        private static void AttachAdmin(BooksController controller, string subject, string email)
        {
            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(new[]
                    {
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.NameIdentifier, subject),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Email, email),
                        new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Role, "Admin")
                    }, "Test"))
                }
            };
        }

        private static InventoryDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<InventoryDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new InventoryDbContext(options);
        }

        private sealed class RecordingBookEventPublisher : IBookEventPublisher
        {
            public List<BookEvent> Events { get; } = new();

            public Task PublishAsync(BookEvent bookEvent, CancellationToken cancellationToken = default)
            {
                Events.Add(bookEvent);
                return Task.CompletedTask;
            }
        }
    }
}
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
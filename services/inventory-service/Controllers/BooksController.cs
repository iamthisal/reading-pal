using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Kafka;
using InventoryService.Models;
using Microsoft.Extensions.Logging;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly InventoryDbContext _context;
        private readonly IBookEventPublisher _bookEventPublisher;
        private readonly ILogger<BooksController> _logger;

        public BooksController(InventoryDbContext context)
            : this(context, new NoOpBookEventPublisher(), LoggerFactory.Create(_ => { }).CreateLogger<BooksController>())
        {
        }

        [ActivatorUtilitiesConstructor]
        public BooksController(
            InventoryDbContext context,
            IBookEventPublisher bookEventPublisher,
            ILogger<BooksController> logger)
        {
            _context = context;
            _bookEventPublisher = bookEventPublisher;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookResponse>>> GetAll()
        {
            var books = await _context.Books
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(books.Select(ToBookResponse));
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BookResponse>> GetById(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            return Ok(ToBookResponse(book));
        }

        [HttpPost("{id:int}/checkouts/{reservationId:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Checkout(int id, int reservationId, CancellationToken cancellationToken)
        {
            if (id <= 0 || reservationId <= 0)
                return BadRequest(new { message = "Book and reservation IDs must be positive." });

            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            var previous = await _context.BookCheckouts.AsNoTracking()
                .SingleOrDefaultAsync(c => c.Id == reservationId, cancellationToken);
            if (previous != null)
                return previous.BookId == id
                    ? Ok(new { bookId = id, reservationId, previous.CheckoutDateUtc })
                    : Conflict(new { message = "This reservation already checked out a different book." });

            var checkoutDateUtc = DateTime.UtcNow;
            var updated = await _context.Books.Where(b => b.Id == id && b.AvailableCopies > 0)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(b => b.AvailableCopies, b => b.AvailableCopies - 1)
                    .SetProperty(b => b.UpdatedAt, checkoutDateUtc), cancellationToken);
            if (updated == 0)
            {
                var exists = await _context.Books.AnyAsync(b => b.Id == id, cancellationToken);
                return exists
                    ? Conflict(new { message = "No copies are available for checkout." })
                    : NotFound(new { message = "Book not found." });
            }

            _context.BookCheckouts.Add(new BookCheckout { Id = reservationId, BookId = id, CheckoutDateUtc = checkoutDateUtc });
            // The count and retry record commit together. A failed transaction cannot deduct a copy.
            await _context.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { bookId = id, reservationId, checkoutDateUtc });
        }

        [HttpPost("{id:int}/checkouts/{reservationId:int}/return")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> ReturnCheckout(int id, int reservationId, CancellationToken cancellationToken)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);
            // The conditional update locks the checkout row and makes concurrent retries harmless.
            var changed = await _context.BookCheckouts
                .Where(c => c.Id == reservationId && c.BookId == id && c.ReturnDateUtc == null)
                .ExecuteUpdateAsync(s => s.SetProperty(c => c.ReturnDateUtc, DateTime.UtcNow), cancellationToken);
            if (changed == 0)
            {
                var previous = await _context.BookCheckouts.AsNoTracking()
                    .SingleOrDefaultAsync(c => c.Id == reservationId && c.BookId == id, cancellationToken);
                return previous?.ReturnDateUtc != null
                    ? Ok(new { bookId = id, reservationId })
                    : NotFound(new { message = "Matching inventory checkout not found." });
            }
            var restored = await _context.Books.Where(b => b.Id == id && b.AvailableCopies < b.TotalCopies)
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.AvailableCopies, b => b.AvailableCopies + 1)
                    .SetProperty(b => b.UpdatedAt, DateTime.UtcNow), cancellationToken);
            if (restored == 0)
                return Conflict(new { message = "Inventory counts need review before this return can complete." });
            await transaction.CommitAsync(cancellationToken);
            return Ok(new { bookId = id, reservationId });
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            var borrowedCopies = book.TotalCopies - book.AvailableCopies;
            if (borrowedCopies > 0)
            {
                return Conflict(new { message = "A book cannot be removed while copies are currently borrowed." });
            }

            var deletedBook = ToBookResponse(book);
            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

            if (!await PublishEventAsync("book-deleted", deletedBook))
            {
                return KafkaFailure(deletedBook.Id);
            }

            return NoContent();
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BookResponse>> Create([FromBody] CreateBookRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var trimmedIsbn = request.ISBN?.Trim() ?? string.Empty;
            var exists = await _context.Books.AnyAsync(b => b.ISBN.ToLower() == trimmedIsbn.ToLower());
            if (exists)
            {
                return Conflict(new { message = $"A book with ISBN '{request.ISBN}' already exists." });
            }

            var now = DateTime.UtcNow;
            var book = new Book
            {
                Title = request.Title.Trim(),
                Author = request.Author.Trim(),
                ISBN = trimmedIsbn,
                Genre = request.Genre.Trim(),
                CoverImageUrl = NormalizeOptionalUrl(request.CoverImageUrl),
                TotalCopies = request.TotalCopies,
                AvailableCopies = request.TotalCopies,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            var response = ToBookResponse(book);
            if (!await PublishEventAsync("book-created", response))
            {
                return KafkaFailure(book.Id);
            }

            return CreatedAtAction(nameof(GetById), new { id = book.Id }, response);
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BookResponse>> Update(int id, [FromBody] UpdateBookRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            var trimmedIsbn = request.ISBN?.Trim() ?? string.Empty;
            var duplicateIsbn = await _context.Books.AnyAsync(b => b.Id != id && b.ISBN.ToLower() == trimmedIsbn.ToLower());
            if (duplicateIsbn)
            {
                return Conflict(new { message = $"A book with ISBN '{request.ISBN}' already exists." });
            }

            var borrowedCopies = book.TotalCopies - book.AvailableCopies;
            if (request.TotalCopies < borrowedCopies)
            {
                return BadRequest(new { message = $"Total copies cannot be less than currently borrowed copies ({borrowedCopies})." });
            }

            book.Title = request.Title.Trim();
            book.Author = request.Author.Trim();
            book.ISBN = trimmedIsbn;
            book.Genre = request.Genre.Trim();
            book.CoverImageUrl = NormalizeOptionalUrl(request.CoverImageUrl);
            book.AvailableCopies = request.TotalCopies - borrowedCopies;
            book.TotalCopies = request.TotalCopies;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var response = ToBookResponse(book);
            if (!await PublishEventAsync("book-updated", response))
            {
                return KafkaFailure(book.Id);
            }

            return Ok(response);
        }

        [HttpPatch("{id}/mark-unavailable")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BookResponse>> MarkUnavailable(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            var borrowedCopies = book.TotalCopies - book.AvailableCopies;
            book.TotalCopies = borrowedCopies;
            book.AvailableCopies = 0;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var response = ToBookResponse(book);
            if (!await PublishEventAsync("book-updated", response))
            {
                return KafkaFailure(book.Id);
            }

            return Ok(response);
        }

        [HttpPatch("{id}/mark-available")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<BookResponse>> MarkAvailable(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            var borrowedCopies = book.TotalCopies - book.AvailableCopies;
            book.TotalCopies = borrowedCopies + 1;
            book.AvailableCopies = 1;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var response = ToBookResponse(book);
            if (!await PublishEventAsync("book-updated", response))
            {
                return KafkaFailure(book.Id);
            }

            return Ok(response);
        }

        private async Task<bool> PublishEventAsync(string eventType, BookResponse book)
        {
            var bookEvent = new BookEvent
            {
                EventType = eventType,
                BookId = book.Id,
                Book = book,
                TimestampUtc = DateTime.UtcNow
            };

            try
            {
                await _bookEventPublisher.PublishAsync(bookEvent);
                return true;
            }
            catch (Exception exception)
            {
                _logger.LogError(exception, "Failed to publish {EventType} for book {BookId} after the database change was committed.", eventType, book.Id);
                return false;
            }
        }

        private ObjectResult KafkaFailure(int bookId)
        {
            return StatusCode(503, new
            {
                message = "The database change was saved, but the Kafka event could not be published.",
                databaseChangePersisted = true,
                bookId
            });
        }

        private static BookResponse ToBookResponse(Book book)
        {
            return new BookResponse
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                Genre = book.Genre,
                CoverImageUrl = book.CoverImageUrl,
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                IsAvailable = book.AvailableCopies > 0,
                AvailabilityStatus = book.AvailableCopies > 0 ? "Available" : "Not available now",
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };
        }

        private static string? NormalizeOptionalUrl(string? url)
        {
            var trimmedUrl = url?.Trim();
            return string.IsNullOrWhiteSpace(trimmedUrl) ? null : trimmedUrl;
        }
    }
}

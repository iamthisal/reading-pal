using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;

namespace InventoryService.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BooksController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public BooksController(InventoryDbContext context)
        {
            _context = context;
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

            _context.Books.Remove(book);
            await _context.SaveChangesAsync();

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
                TotalCopies = request.TotalCopies,
                AvailableCopies = request.TotalCopies,
                CreatedAt = now,
                UpdatedAt = now
            };

            _context.Books.Add(book);
            await _context.SaveChangesAsync();

            var response = ToBookResponse(book);

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
            book.AvailableCopies = request.TotalCopies - borrowedCopies;
            book.TotalCopies = request.TotalCopies;
            book.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Ok(ToBookResponse(book));
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

            return Ok(ToBookResponse(book));
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

            return Ok(ToBookResponse(book));
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
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                IsAvailable = book.AvailableCopies > 0,
                AvailabilityStatus = book.AvailableCopies > 0 ? "Available" : "Not available now",
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };
        }
    }
}

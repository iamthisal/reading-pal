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
                .Select(b => new BookResponse
                {
                    Id = b.Id,
                    Title = b.Title,
                    Author = b.Author,
                    ISBN = b.ISBN,
                    Genre = b.Genre,
                    TotalCopies = b.TotalCopies,
                    AvailableCopies = b.AvailableCopies,
                    CreatedAt = b.CreatedAt,
                    UpdatedAt = b.UpdatedAt
                })
                .ToListAsync();

            return Ok(books);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<BookResponse>> GetById(int id)
        {
            var book = await _context.Books.FindAsync(id);
            if (book == null)
            {
                return NotFound(new { message = $"Book with ID {id} not found." });
            }

            return Ok(new BookResponse
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                Genre = book.Genre,
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            });
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

            var response = new BookResponse
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                Genre = book.Genre,
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };

            return CreatedAtAction(nameof(GetById), new { id = book.Id }, response);
        }
    }
}

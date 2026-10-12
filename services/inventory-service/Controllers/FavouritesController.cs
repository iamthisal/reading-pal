using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;

namespace InventoryService.Controllers
{
    /// <summary>The signed-in reader's own favourite books. Nobody can see or change another reader's.</summary>
    [ApiController]
    [Authorize]
    [Route("api/[controller]")]
    public class FavouritesController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public FavouritesController(InventoryDbContext context)
        {
            _context = context;
        }

        /// <summary>The reader's favourite books, most recently added first.</summary>
        [HttpGet]
        public async Task<ActionResult<IEnumerable<BookResponse>>> GetAll()
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var books = await _context.FavouriteBooks
                .Where(favourite => favourite.UserId == userId)
                .OrderByDescending(favourite => favourite.CreatedAt)
                .ThenByDescending(favourite => favourite.Id)
                .Select(favourite => favourite.Book!)
                .ToListAsync();

            return Ok(books.Select(BookResponse.FromBook));
        }

        /// <summary>Just the IDs, for marking the hearts on a page of books.</summary>
        [HttpGet("ids")]
        public async Task<ActionResult<IEnumerable<int>>> GetIds()
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            return Ok(await _context.FavouriteBooks
                .Where(favourite => favourite.UserId == userId)
                .Select(favourite => favourite.BookId)
                .ToListAsync());
        }

        /// <summary>Marks a book as a favourite. Doing it twice is harmless.</summary>
        [HttpPut("{bookId:int}")]
        public async Task<IActionResult> Add(int bookId)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            if (!await _context.Books.AnyAsync(book => book.Id == bookId))
            {
                return NotFound(new { message = $"Book with ID {bookId} not found." });
            }

            if (!await _context.FavouriteBooks.AnyAsync(favourite => favourite.UserId == userId && favourite.BookId == bookId))
            {
                _context.FavouriteBooks.Add(new FavouriteBook { UserId = userId, BookId = bookId, CreatedAt = DateTime.UtcNow });
                try
                {
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateException)
                {
                    // Two quick presses of the heart: the other request saved it first, which is the outcome we wanted.
                    _context.ChangeTracker.Clear();
                    if (!await _context.FavouriteBooks.AnyAsync(favourite => favourite.UserId == userId && favourite.BookId == bookId))
                    {
                        throw;
                    }
                }
            }

            return NoContent();
        }

        /// <summary>Removes a book from the reader's favourites. Removing one that is not there is harmless.</summary>
        [HttpDelete("{bookId:int}")]
        public async Task<IActionResult> Remove(int bookId)
        {
            var userId = CurrentUserId();
            if (userId == null)
            {
                return Unauthorized();
            }

            var existing = await _context.FavouriteBooks
                .Where(favourite => favourite.UserId == userId && favourite.BookId == bookId)
                .ToListAsync();
            if (existing.Count > 0)
            {
                _context.FavouriteBooks.RemoveRange(existing);
                await _context.SaveChangesAsync();
            }

            return NoContent();
        }

        private string? CurrentUserId()
        {
            var id = User?.FindFirstValue(ClaimTypes.NameIdentifier) ?? User?.FindFirstValue("sub");
            return string.IsNullOrWhiteSpace(id) ? null : id;
        }
    }
}

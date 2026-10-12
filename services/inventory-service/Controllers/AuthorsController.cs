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
    public class AuthorsController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public AuthorsController(InventoryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<AuthorResponse>>> GetAll()
        {
            var authors = await _context.Authors.OrderBy(a => a.Name).ToListAsync();
            var counts = await BookCountsAsync();

            return Ok(authors.Select(author => ToAuthorResponse(author, counts)));
        }

        [HttpGet("{id:int}")]
        public async Task<ActionResult<AuthorResponse>> GetById(int id)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author == null)
            {
                return NotFound(new { message = $"Author with ID {id} not found." });
            }

            return Ok(ToAuthorResponse(author, await BookCountsAsync()));
        }

        /// <summary>The books carrying this author's name, newest first.</summary>
        [HttpGet("{id:int}/books")]
        public async Task<ActionResult<IEnumerable<BookResponse>>> GetBooks(int id, [FromQuery] int? limit = null)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author == null)
            {
                return NotFound(new { message = $"Author with ID {id} not found." });
            }

            var name = author.Name.ToLower();
            IQueryable<Book> query = _context.Books
                .Where(book => book.Author.ToLower() == name)
                .OrderByDescending(book => book.CreatedAt);
            if (limit is > 0)
            {
                query = query.Take(limit.Value);
            }

            var books = await query.ToListAsync();
            return Ok(books.Select(BookResponse.FromBook));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuthorResponse>> Create([FromBody] CreateAuthorRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var name = request.Name.Trim();
            if (await _context.Authors.AnyAsync(a => a.Name.ToLower() == name.ToLower()))
            {
                return Conflict(new { message = $"An author named '{name}' already exists." });
            }

            var now = DateTime.UtcNow;
            var author = new Author { Name = name, CreatedAt = now, UpdatedAt = now };
            _context.Authors.Add(author);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetById), new { id = author.Id }, ToAuthorResponse(author, await BookCountsAsync()));
        }

        [HttpPut("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<AuthorResponse>> Update(int id, [FromBody] UpdateAuthorRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var author = await _context.Authors.FindAsync(id);
            if (author == null)
            {
                return NotFound(new { message = $"Author with ID {id} not found." });
            }

            var name = request.Name.Trim();
            if (await _context.Authors.AnyAsync(a => a.Id != id && a.Name.ToLower() == name.ToLower()))
            {
                return Conflict(new { message = $"An author named '{name}' already exists." });
            }

            // Books are matched to an author by name, so a rename has to carry over to them.
            var oldName = author.Name;
            author.Name = name;
            author.UpdatedAt = DateTime.UtcNow;

            var booksByAuthor = await _context.Books
                .Where(book => book.Author.ToLower() == oldName.ToLower())
                .ToListAsync();
            foreach (var book in booksByAuthor)
            {
                book.Author = name;
                book.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(ToAuthorResponse(author, await BookCountsAsync()));
        }

        /// <summary>Removes the author entry (and its place on any list). The author's books are kept.</summary>
        [HttpDelete("{id:int}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var author = await _context.Authors.FindAsync(id);
            if (author == null)
            {
                return NotFound(new { message = $"Author with ID {id} not found." });
            }

            _context.Authors.Remove(author);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private async Task<Dictionary<string, int>> BookCountsAsync()
        {
            var names = await _context.Books.Select(book => book.Author).ToListAsync();
            return names
                .GroupBy(name => name.Trim().ToLowerInvariant())
                .ToDictionary(group => group.Key, group => group.Count());
        }

        internal static AuthorResponse ToAuthorResponse(Author author, IReadOnlyDictionary<string, int> bookCounts)
        {
            return new AuthorResponse
            {
                Id = author.Id,
                Name = author.Name,
                BookCount = bookCounts.TryGetValue(author.Name.Trim().ToLowerInvariant(), out var count) ? count : 0,
                CreatedAt = author.CreatedAt,
                UpdatedAt = author.UpdatedAt
            };
        }
    }
}

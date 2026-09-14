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
    public class GenresController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public GenresController(InventoryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<GenreResponse>>> GetAll()
        {
            var genres = await _context.Genres
                .OrderBy(g => g.Name)
                .Select(g => ToGenreResponse(g))
                .ToListAsync();

            return Ok(genres);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GenreResponse>> Create([FromBody] CreateGenreRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var name = request.Name.Trim();
            if (await _context.Genres.AnyAsync(g => g.Name.ToLower() == name.ToLower()))
            {
                return Conflict(new { message = $"A genre named '{name}' already exists." });
            }

            var now = DateTime.UtcNow;
            var genre = new Genre { Name = name, CreatedAt = now, UpdatedAt = now };
            _context.Genres.Add(genre);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetAll), new { id = genre.Id }, ToGenreResponse(genre));
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<GenreResponse>> Update(int id, [FromBody] UpdateGenreRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                return NotFound(new { message = $"Genre with ID {id} not found." });
            }

            var name = request.Name.Trim();
            if (await _context.Genres.AnyAsync(g => g.Id != id && g.Name.ToLower() == name.ToLower()))
            {
                return Conflict(new { message = $"A genre named '{name}' already exists." });
            }

            var oldName = genre.Name;
            genre.Name = name;
            genre.UpdatedAt = DateTime.UtcNow;

            var booksWithGenre = await _context.Books
                .Where(book => book.Genre.ToLower() == oldName.ToLower())
                .ToListAsync();
            foreach (var book in booksWithGenre)
            {
                book.Genre = name;
                book.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();
            return Ok(ToGenreResponse(genre));
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(int id)
        {
            var genre = await _context.Genres.FindAsync(id);
            if (genre == null)
            {
                return NotFound(new { message = $"Genre with ID {id} not found." });
            }

            var inUse = await _context.Books.AnyAsync(book => book.Genre.ToLower() == genre.Name.ToLower());
            if (inUse)
            {
                return Conflict(new { message = "This genre cannot be deleted while it is assigned to a book." });
            }

            _context.Genres.Remove(genre);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private static GenreResponse ToGenreResponse(Genre genre)
        {
            return new GenreResponse
            {
                Id = genre.Id,
                Name = genre.Name,
                CreatedAt = genre.CreatedAt,
                UpdatedAt = genre.UpdatedAt
            };
        }
    }
}

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;

namespace InventoryService.Controllers
{
    /// <summary>
    /// Curated lists ("Top 100", "Popular Authors"). Anyone can read them; admins manage them.
    /// </summary>
    [ApiController]
    [Route("api/[controller]")]
    public class ListsController : ControllerBase
    {
        private readonly InventoryDbContext _context;

        public ListsController(InventoryDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<CuratedListSummaryResponse>>> GetAll()
        {
            var lists = await _context.CuratedLists
                .OrderBy(list => list.Name)
                .Select(list => new { List = list, ItemCount = list.Items.Count })
                .ToListAsync();

            return Ok(lists.Select(entry => ToSummary(entry.List, entry.ItemCount)));
        }

        /// <summary>One list with its books or authors in order. Use limit to fetch only the first few.</summary>
        [HttpGet("{key}")]
        public async Task<ActionResult<CuratedListResponse>> GetByKey(string key, [FromQuery] int? limit = null)
        {
            var list = await FindAsync(key);
            if (list == null)
            {
                return NotFound(new { message = $"List '{key}' not found." });
            }

            return Ok(await ToResponseAsync(list, limit));
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CuratedListResponse>> Create([FromBody] CreateCuratedListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var key = request.Key.Trim().ToLowerInvariant();
            if (!Enum.TryParse<CuratedListItemType>(request.ItemType.Trim(), ignoreCase: true, out var itemType)
                || !Enum.IsDefined(itemType))
            {
                return BadRequest(new { message = "Item type must be Books or Authors." });
            }

            if (await _context.CuratedLists.AnyAsync(list => list.Key == key))
            {
                return Conflict(new { message = $"A list with the key '{key}' already exists." });
            }

            var now = DateTime.UtcNow;
            var created = new CuratedList { Key = key, Name = request.Name.Trim(), ItemType = itemType, CreatedAt = now, UpdatedAt = now };
            _context.CuratedLists.Add(created);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetByKey), new { key = created.Key }, await ToResponseAsync(created, null));
        }

        /// <summary>Renames a list. Its key and item type stay fixed so pages that use it keep working.</summary>
        [HttpPut("{key}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CuratedListResponse>> Update(string key, [FromBody] UpdateCuratedListRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var list = await FindAsync(key);
            if (list == null)
            {
                return NotFound(new { message = $"List '{key}' not found." });
            }

            list.Name = request.Name.Trim();
            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(await ToResponseAsync(list, null));
        }

        /// <summary>Replaces the list's contents with the given IDs, in the given order.</summary>
        [HttpPut("{key}/items")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<CuratedListResponse>> SetItems(string key, [FromBody] SetCuratedListItemsRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var list = await FindAsync(key);
            if (list == null)
            {
                return NotFound(new { message = $"List '{key}' not found." });
            }

            var ids = request.ItemIds;
            if (ids.Count != ids.Distinct().Count())
            {
                return BadRequest(new { message = "The same item cannot appear on a list twice." });
            }

            var known = list.ItemType == CuratedListItemType.Books
                ? await _context.Books.Where(book => ids.Contains(book.Id)).Select(book => book.Id).ToListAsync()
                : await _context.Authors.Where(author => ids.Contains(author.Id)).Select(author => author.Id).ToListAsync();
            var missing = ids.Except(known).ToList();
            if (missing.Count > 0)
            {
                var kind = list.ItemType == CuratedListItemType.Books ? "book" : "author";
                return BadRequest(new { message = $"Unknown {kind} ID(s): {string.Join(", ", missing)}." });
            }

            var existing = await _context.CuratedListItems.Where(item => item.CuratedListId == list.Id).ToListAsync();
            _context.CuratedListItems.RemoveRange(existing);
            // Remove first so the replacement rows cannot collide with the unique (list, item) index.
            await _context.SaveChangesAsync();

            for (var position = 0; position < ids.Count; position++)
            {
                _context.CuratedListItems.Add(new CuratedListItem
                {
                    CuratedListId = list.Id,
                    Position = position,
                    BookId = list.ItemType == CuratedListItemType.Books ? ids[position] : null,
                    AuthorId = list.ItemType == CuratedListItemType.Authors ? ids[position] : null
                });
            }

            list.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();

            return Ok(await ToResponseAsync(list, null));
        }

        [HttpDelete("{key}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string key)
        {
            var list = await FindAsync(key);
            if (list == null)
            {
                return NotFound(new { message = $"List '{key}' not found." });
            }

            _context.CuratedLists.Remove(list);
            await _context.SaveChangesAsync();
            return NoContent();
        }

        private Task<CuratedList?> FindAsync(string key)
        {
            var normalized = (key ?? string.Empty).Trim().ToLowerInvariant();
            return _context.CuratedLists.FirstOrDefaultAsync(list => list.Key == normalized);
        }

        private async Task<CuratedListResponse> ToResponseAsync(CuratedList list, int? limit)
        {
            var itemCount = await _context.CuratedListItems.CountAsync(item => item.CuratedListId == list.Id);
            IQueryable<CuratedListItem> items = _context.CuratedListItems
                .Where(item => item.CuratedListId == list.Id)
                .OrderBy(item => item.Position);
            if (limit is > 0)
            {
                items = items.Take(limit.Value);
            }

            var summary = ToSummary(list, itemCount);
            var response = new CuratedListResponse
            {
                Id = summary.Id,
                Key = summary.Key,
                Name = summary.Name,
                ItemType = summary.ItemType,
                ItemCount = summary.ItemCount,
                CreatedAt = summary.CreatedAt,
                UpdatedAt = summary.UpdatedAt
            };

            if (list.ItemType == CuratedListItemType.Books)
            {
                var books = await items.Include(item => item.Book).ToListAsync();
                response.Books = books.Where(item => item.Book != null).Select(item => BookResponse.FromBook(item.Book!)).ToList();
            }
            else
            {
                var authors = await items.Include(item => item.Author).ToListAsync();
                var names = await _context.Books.Select(book => book.Author).ToListAsync();
                var counts = names
                    .GroupBy(name => name.Trim().ToLowerInvariant())
                    .ToDictionary(group => group.Key, group => group.Count());
                response.Authors = authors.Where(item => item.Author != null)
                    .Select(item => AuthorsController.ToAuthorResponse(item.Author!, counts)).ToList();
            }

            return response;
        }

        private static CuratedListSummaryResponse ToSummary(CuratedList list, int itemCount)
        {
            return new CuratedListSummaryResponse
            {
                Id = list.Id,
                Key = list.Key,
                Name = list.Name,
                ItemType = list.ItemType.ToString(),
                ItemCount = itemCount,
                CreatedAt = list.CreatedAt,
                UpdatedAt = list.UpdatedAt
            };
        }
    }
}

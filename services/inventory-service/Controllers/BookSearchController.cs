using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Models;

namespace InventoryService.Controllers
{
    /// <summary>Catalogue search: free text, category and availability filters, sorting and paging.</summary>
    [ApiController]
    [Route("api/books/search")]
    public class BookSearchController : ControllerBase
    {
        public const int DefaultPageSize = 12;
        public const int MaxPageSize = 48;
        private const int MaxTerms = 8;

        private readonly InventoryDbContext _context;

        public BookSearchController(InventoryDbContext context)
        {
            _context = context;
        }

        /// <param name="q">Words to look for in the title, author, category or ISBN. Every word must match somewhere.</param>
        /// <param name="genres">Only these categories (any of them). Repeat the parameter for several.</param>
        /// <param name="availableOnly">Only books with a copy on the shelf.</param>
        /// <param name="sort">relevance (default with a query), newest (default without), title, author.</param>
        [HttpGet]
        public async Task<ActionResult<BookSearchResponse>> Search(
            [FromQuery] string? q = null,
            [FromQuery] string[]? genres = null,
            [FromQuery] bool availableOnly = false,
            [FromQuery] string? sort = null,
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = DefaultPageSize)
        {
            var phrase = (q ?? string.Empty).Trim().ToLower();
            var terms = phrase.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Distinct().Take(MaxTerms).ToList();
            var wantedGenres = (genres ?? Array.Empty<string>())
                .Select(genre => genre.Trim().ToLower()).Where(genre => genre.Length > 0).Distinct().ToList();
            pageSize = Math.Clamp(pageSize <= 0 ? DefaultPageSize : pageSize, 1, MaxPageSize);

            IQueryable<Book> matchingText = _context.Books.AsNoTracking();
            foreach (var term in terms)
            {
                matchingText = matchingText.Where(book =>
                    book.Title.ToLower().Contains(term) ||
                    book.Author.ToLower().Contains(term) ||
                    book.Genre.ToLower().Contains(term) ||
                    book.ISBN.ToLower().Contains(term));
            }

            IQueryable<Book> InGenres(IQueryable<Book> books) =>
                wantedGenres.Count == 0 ? books : books.Where(book => wantedGenres.Contains(book.Genre.ToLower()));
            IQueryable<Book> Available(IQueryable<Book> books) =>
                availableOnly ? books.Where(book => book.AvailableCopies > 0) : books;

            var results = Available(InGenres(matchingText));
            var total = await results.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            // Asking for a page past the end (after narrowing the filters, say) shows the last page instead of nothing.
            page = Math.Clamp(page, 1, totalPages);

            var items = await Sort(results, sort, phrase)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync();

            // Each filter's counts leave that filter itself out, so the reader sees what ticking another box would add.
            var genreCounts = await Available(matchingText)
                .GroupBy(book => book.Genre)
                .Select(group => new GenreFacet { Name = group.Key, Count = group.Count() })
                .ToListAsync();

            return Ok(new BookSearchResponse
            {
                Items = items.Select(BookResponse.FromBook).ToList(),
                Total = total,
                Page = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                Facets = new BookSearchFacets
                {
                    Genres = genreCounts.OrderBy(facet => facet.Name, StringComparer.OrdinalIgnoreCase).ToList(),
                    AvailableCount = await InGenres(matchingText).CountAsync(book => book.AvailableCopies > 0)
                }
            });
        }

        private static IQueryable<Book> Sort(IQueryable<Book> books, string? sort, string phrase)
        {
            switch ((sort ?? string.Empty).Trim().ToLower())
            {
                case "title":
                    return books.OrderBy(book => book.Title).ThenBy(book => book.Id);
                case "author":
                    return books.OrderBy(book => book.Author).ThenBy(book => book.Title).ThenBy(book => book.Id);
                case "newest":
                    return books.OrderByDescending(book => book.CreatedAt).ThenByDescending(book => book.Id);
            }

            if (phrase.Length == 0)
            {
                return books.OrderByDescending(book => book.CreatedAt).ThenByDescending(book => book.Id);
            }

            // Relevance: a title that starts with what was typed, then titles containing it, then author matches, then the rest.
            return books
                .OrderBy(book =>
                    book.Title.ToLower().StartsWith(phrase) ? 0 :
                    book.Title.ToLower().Contains(phrase) ? 1 :
                    book.Author.ToLower().Contains(phrase) ? 2 : 3)
                .ThenBy(book => book.Title)
                .ThenBy(book => book.Id);
        }
    }
}

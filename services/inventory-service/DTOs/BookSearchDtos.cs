using System.Collections.Generic;

namespace InventoryService.DTOs
{
    public class BookSearchResponse
    {
        public List<BookResponse> Items { get; set; } = new();
        /// <summary>How many books match the search and filters in total, across all pages.</summary>
        public int Total { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public BookSearchFacets Facets { get; set; } = new();
    }

    /// <summary>What each filter would give, so the page can show counts next to its checkboxes.</summary>
    public class BookSearchFacets
    {
        /// <summary>Every category with matches for the search text and availability filter, ignoring the category filter itself.</summary>
        public List<GenreFacet> Genres { get; set; } = new();
        /// <summary>Matches for the search text and category filter that can be borrowed right now.</summary>
        public int AvailableCount { get; set; }
    }

    public class GenreFacet
    {
        public string Name { get; set; } = string.Empty;
        public int Count { get; set; }
    }
}

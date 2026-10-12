using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class CuratedListSummaryResponse
    {
        public int Id { get; set; }
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        /// <summary>"Books" or "Authors".</summary>
        public string ItemType { get; set; } = string.Empty;
        public int ItemCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>A list with its entries in order. Only the collection matching ItemType is filled.</summary>
    public class CuratedListResponse : CuratedListSummaryResponse
    {
        public List<BookResponse> Books { get; set; } = new();
        public List<AuthorResponse> Authors { get; set; } = new();
    }

    public class CreateCuratedListRequest
    {
        [Required(ErrorMessage = "List key is required")]
        [RegularExpression("^[a-z0-9]+(-[a-z0-9]+)*$", ErrorMessage = "List key may only contain lowercase letters, numbers and single hyphens, e.g. top-100")]
        [StringLength(80, MinimumLength = 1, ErrorMessage = "List key must be between 1 and 80 characters")]
        public string Key { get; set; } = string.Empty;

        [Required(ErrorMessage = "List name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "List name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "Item type is required")]
        [RegularExpression("^(?i)(Books|Authors)$", ErrorMessage = "Item type must be Books or Authors")]
        public string ItemType { get; set; } = "Books";
    }

    public class UpdateCuratedListRequest
    {
        [Required(ErrorMessage = "List name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "List name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>The full, ordered contents of a list: book IDs or author IDs depending on its item type.</summary>
    public class SetCuratedListItemsRequest
    {
        [Required(ErrorMessage = "Item IDs are required")]
        [MaxLength(500, ErrorMessage = "A list can hold at most 500 items")]
        public List<int> ItemIds { get; set; } = new();
    }
}

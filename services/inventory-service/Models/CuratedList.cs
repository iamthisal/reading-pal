using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models
{
    public enum CuratedListItemType
    {
        Books,
        Authors
    }

    /// <summary>
    /// A hand-picked, ordered list such as "Top 100" or "Popular Authors".
    /// The frontend asks for a list by its key and decides where to show it.
    /// </summary>
    public class CuratedList
    {
        public int Id { get; set; }

        /// <summary>Stable identifier used in URLs, e.g. "top-100".</summary>
        [Required]
        [MaxLength(80)]
        public string Key { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>A list holds either books or authors, never a mix.</summary>
        public CuratedListItemType ItemType { get; set; } = CuratedListItemType.Books;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<CuratedListItem> Items { get; set; } = new();
    }
}

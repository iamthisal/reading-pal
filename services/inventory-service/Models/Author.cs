using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models
{
    /// <summary>
    /// An author the library wants to feature. Books still store their author as text;
    /// a book belongs to an author when the names match (case-insensitively).
    /// </summary>
    public class Author
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        /// <summary>A short biography.</summary>
        [MaxLength(2000)]
        public string? Description { get; set; }

        /// <summary>A link to a portrait of the author.</summary>
        [MaxLength(500)]
        public string? ImageUrl { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    }
}

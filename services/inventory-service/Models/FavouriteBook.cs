using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.Models
{
    /// <summary>A book a reader has marked as a favourite. The user lives in the user service, so only their ID is kept.</summary>
    public class FavouriteBook
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string UserId { get; set; } = string.Empty;

        public int BookId { get; set; }
        public Book? Book { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}

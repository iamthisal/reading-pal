using System;

namespace InventoryService.DTOs
{
    public class BookResponse
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string ISBN { get; set; } = string.Empty;
        public string Genre { get; set; } = string.Empty;
        public string? CoverImageUrl { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

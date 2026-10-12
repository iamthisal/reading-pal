using System;
using InventoryService.Models;

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
        public string? Description { get; set; }
        public int TotalCopies { get; set; }
        public int AvailableCopies { get; set; }
        public bool IsAvailable { get; set; }
        public string AvailabilityStatus { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static BookResponse FromBook(Book book)
        {
            return new BookResponse
            {
                Id = book.Id,
                Title = book.Title,
                Author = book.Author,
                ISBN = book.ISBN,
                Genre = book.Genre,
                CoverImageUrl = book.CoverImageUrl,
                Description = book.Description,
                TotalCopies = book.TotalCopies,
                AvailableCopies = book.AvailableCopies,
                IsAvailable = book.AvailableCopies > 0,
                AvailabilityStatus = book.AvailableCopies > 0 ? "Available" : "Not available now",
                CreatedAt = book.CreatedAt,
                UpdatedAt = book.UpdatedAt
            };
        }
    }
}

using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class UpdateBookRequest
    {
        [Required(ErrorMessage = "Title is required")]
        [StringLength(200, MinimumLength = 1, ErrorMessage = "Title must be between 1 and 200 characters")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Author is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Author must be between 1 and 150 characters")]
        public string Author { get; set; } = string.Empty;

        [Required(ErrorMessage = "ISBN is required")]
        [StringLength(30, MinimumLength = 1, ErrorMessage = "ISBN must be between 1 and 30 characters")]
        public string ISBN { get; set; } = string.Empty;

        [Required(ErrorMessage = "Genre is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Genre must be between 1 and 100 characters")]
        public string Genre { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "Cover image URL cannot exceed 500 characters")]
        [Url(ErrorMessage = "Cover image URL must be a valid URL")]
        public string? CoverImageUrl { get; set; }

        [Required(ErrorMessage = "Total copies is required")]
        [Range(0, 100000, ErrorMessage = "Total copies cannot be negative")]
        public int TotalCopies { get; set; }
    }
}

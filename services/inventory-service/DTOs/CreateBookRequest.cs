using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class CreateBookRequest
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

        [Required(ErrorMessage = "Total copies is required")]
        [Range(1, 100000, ErrorMessage = "Total copies must be at least 1")]
        public int TotalCopies { get; set; }
    }
}

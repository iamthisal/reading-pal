using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class CreateGenreRequest
    {
        [Required(ErrorMessage = "Genre name is required")]
        [StringLength(100, MinimumLength = 1, ErrorMessage = "Genre name must be between 1 and 100 characters")]
        public string Name { get; set; } = string.Empty;
    }
}

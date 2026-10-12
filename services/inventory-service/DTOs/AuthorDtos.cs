using System;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class AuthorResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        /// <summary>How many catalogue books carry this author's name.</summary>
        public int BookCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class CreateAuthorRequest
    {
        [Required(ErrorMessage = "Author name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Author name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;
    }

    public class UpdateAuthorRequest
    {
        [Required(ErrorMessage = "Author name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Author name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;
    }
}

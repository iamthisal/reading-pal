using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace InventoryService.DTOs
{
    public class AuthorResponse
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        /// <summary>How many catalogue books carry this author's name.</summary>
        public int BookCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class AuthorProfileResponse
    {
        /// <summary>Null when the author has books but no author entry of their own.</summary>
        public int? Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? ImageUrl { get; set; }
        public int BookCount { get; set; }
        public List<BookResponse> Books { get; set; } = new();
    }

    public class CreateAuthorRequest
    {
        [Required(ErrorMessage = "Author name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Author name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        // Empty is allowed: on an update it means "remove the portrait".
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
        [RegularExpression(@"^\s*$|^\s*https?://\S+\s*$", ErrorMessage = "Image URL must be a valid URL")]
        public string? ImageUrl { get; set; }
    }

    public class UpdateAuthorRequest
    {
        [Required(ErrorMessage = "Author name is required")]
        [StringLength(150, MinimumLength = 1, ErrorMessage = "Author name must be between 1 and 150 characters")]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000, ErrorMessage = "Description cannot exceed 2000 characters")]
        public string? Description { get; set; }

        // Empty is allowed: on an update it means "remove the portrait".
        [StringLength(500, ErrorMessage = "Image URL cannot exceed 500 characters")]
        [RegularExpression(@"^\s*$|^\s*https?://\S+\s*$", ErrorMessage = "Image URL must be a valid URL")]
        public string? ImageUrl { get; set; }
    }
}

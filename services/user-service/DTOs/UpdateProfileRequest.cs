using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs
{
    public class UpdateProfileRequest
    {
        [Required]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [MinLength(6)]
        public string? Password { get; set; }
    }
}

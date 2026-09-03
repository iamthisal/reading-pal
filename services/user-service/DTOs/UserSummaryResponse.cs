using System;

namespace UserService.DTOs
{
    public class UserSummaryResponse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public bool IsValidated { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

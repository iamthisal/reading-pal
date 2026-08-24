namespace UserService.DTOs
{
    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public bool IsValidated { get; set; }
    }
}

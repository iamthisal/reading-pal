namespace UserService.DTOs
{
    public class UserProfileResponse
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        /// <summary>Whether an admin has approved this account, read from the database right now (a sign-in token can be out of date).</summary>
        public bool IsValidated { get; set; }
    }
}

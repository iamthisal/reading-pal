namespace NotificationService.Models;

/// <summary>
/// Records that one recipient has read a shared notification: a Customers announcement, or an admin
/// notification (each admin has their own read state). See <see cref="RecipientKeys"/>.
/// </summary>
public sealed class NotificationRead
{
    public int Id { get; set; }
    public int NotificationId { get; set; }
    public string RecipientKey { get; set; } = string.Empty;
    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>Identifies a reader across customers (numeric user IDs) and admins (token subject).</summary>
public static class RecipientKeys
{
    public static string Customer(int userId) => $"user:{userId}";
    public static string Admin(string subject) => $"admin:{subject}";
}

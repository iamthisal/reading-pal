namespace NotificationService.Models;

/// <summary>Records that one customer has read a Customers announcement (announcements are shared rows).</summary>
public sealed class NotificationRead
{
    public int Id { get; set; }
    public int NotificationId { get; set; }
    public int UserId { get; set; }
    public DateTime ReadAtUtc { get; set; } = DateTime.UtcNow;
}

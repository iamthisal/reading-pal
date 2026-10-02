namespace NotificationService.Models;

public sealed class Notification
{
    public int Id { get; set; }
    // The Lending event that produced this notification. Unique, so a redelivered event cannot create a duplicate.
    public Guid EventId { get; set; }
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int ReservationId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public static class NotificationTypes
{
    public const string ReservationAccepted = "ReservationAccepted";
    public const string ReservationCancelled = "ReservationCancelled";
    public const string BookReturned = "BookReturned";
}

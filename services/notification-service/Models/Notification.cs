namespace NotificationService.Models;

public sealed class Notification
{
    public int Id { get; set; }
    // The Lending event that produced this notification. Unique per audience, so a redelivered event
    // cannot create a duplicate, while one event may notify both the customer and the admins.
    public Guid EventId { get; set; }
    // Who receives it: one customer (User), the library admins (Admin), or every eligible customer (Customers).
    public string Audience { get; set; } = NotificationAudiences.User;
    // The customer the notification is about. For Audience = User they are also the recipient.
    // 0 for Customers announcements, whose per-customer read state lives in NotificationReads.
    public int UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public int ReservationId { get; set; }
    public int BookId { get; set; }
    public string BookTitle { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime? ReservationDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public static class NotificationAudiences
{
    public const string User = "User";
    public const string Admin = "Admin";
    // A catalogue announcement shown to customers who were registered and active when it was made.
    public const string Customers = "Customers";
}

public static class NotificationTypes
{
    public const string ReservationAccepted = "ReservationAccepted";
    public const string ReservationCancelled = "ReservationCancelled";
    public const string BookReturned = "BookReturned";
    public const string FineRecorded = "FineRecorded";
    public const string DueDateReminder = "DueDateReminder";
    public const string NewBook = "NewBook";
    public const string BookDeleted = "BookDeleted";
    public const string NewReservation = "NewReservation";
    public const string CustomerCancelledReservation = "CustomerCancelledReservation";
}

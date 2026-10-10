namespace NotificationService.Models;

public sealed class Notification
{
    public int Id { get; set; }
    // The Lending event that produced this notification. Unique per audience, so a redelivered event
    // cannot create a duplicate, while one event may notify both the customer and the admins.
    public Guid EventId { get; set; }
    // Who receives it: the customer (User) or the library admins (Admin).
    public string Audience { get; set; } = NotificationAudiences.User;
    // The customer the notification is about. For Audience = User they are also the recipient.
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
}

public static class NotificationTypes
{
    public const string ReservationAccepted = "ReservationAccepted";
    public const string ReservationCancelled = "ReservationCancelled";
    public const string BookReturned = "BookReturned";
    public const string FineRecorded = "FineRecorded";
    public const string DueDateReminder = "DueDateReminder";
    public const string NewReservation = "NewReservation";
    public const string CustomerCancelledReservation = "CustomerCancelledReservation";
}

namespace NotificationService.Kafka;

/// <summary>
/// Shape of the events Lending publishes (reservation-created, reservation-accepted, reservation-cancelled, book-returned,
/// fine-recorded, book-due-soon, reservation-snapshot). One class covers all of them; fields an event type does not carry stay null. Schema version 1 events
/// (published before due dates and CancelledBy were added) are still accepted.
/// </summary>
public sealed class LendingEvent
{
    public Guid EventId { get; init; }
    public string? EventType { get; init; }
    public int SchemaVersion { get; init; }
    public DateTime TimestampUtc { get; init; }
    public int ReservationId { get; init; }
    public int UserId { get; init; }
    public int BookId { get; init; }
    public DateTime? ReservationDate { get; init; }
    public DateTime? DueDate { get; init; }
    public DateTime? ReturnDate { get; init; }
    public string? CancelledBy { get; init; }
    public int? DaysOverdue { get; init; }
    public decimal? Amount { get; init; }
    public int? DaysUntilDue { get; init; }
    // Lending's reservation status, carried by reservation-snapshot.
    public string? Status { get; init; }
}

public static class LendingEventTypes
{
    public const string ReservationAccepted = "reservation-accepted";
    public const string ReservationCancelled = "reservation-cancelled";
    public const string BookReturned = "book-returned";
    public const string ReservationCreated = "reservation-created";
    public const string FineRecorded = "fine-recorded";
    public const string BookDueSoon = "book-due-soon";
    public const string ReservationSnapshot = "reservation-snapshot";
}

namespace LendingService.Kafka;

/// <summary>Published once per loan when its due date is near (see DueDateReminderService).</summary>
public sealed class BookDueSoonEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "book-due-soon";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int BorrowRecordId { get; init; }
    public int BookId { get; init; }
    public int UserId { get; init; }
    public DateTime DueDate { get; init; }
    // Sri Lankan calendar days left when the reminder was created (2, or fewer when catching up).
    public int DaysUntilDue { get; init; }
}

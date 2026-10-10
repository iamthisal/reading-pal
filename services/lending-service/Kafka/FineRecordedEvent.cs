namespace LendingService.Kafka;

/// <summary>
/// Published when an actual unpaid fine is saved for a late return. Estimated fines shown for active
/// loans are calculated on the fly and never produce this event.
/// </summary>
public sealed class FineRecordedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "fine-recorded";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    // A loan has at most one fine, so the borrow record identifies the fine.
    public int BorrowRecordId { get; init; }
    public int BookId { get; init; }
    public int UserId { get; init; }
    public int DaysOverdue { get; init; }
    public decimal DailyRate { get; init; }
    public decimal Amount { get; init; }
    public string FineStatus { get; init; } = "Unpaid";
    public DateTime ReturnDate { get; init; }
}

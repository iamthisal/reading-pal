namespace LendingService.Kafka;

public sealed class BookReturnedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "book-returned";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int BorrowRecordId { get; init; }
    public int BookId { get; init; }
    public int UserId { get; init; }
    public DateTime ReturnDate { get; init; }
}

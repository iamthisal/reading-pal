namespace LendingService.Kafka;

public sealed class ReservationAcceptedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "reservation-accepted";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int UserId { get; init; }
    public int BookId { get; init; }
    public DateTime ReservationDate { get; init; }
    public string Status { get; init; } = "Borrowed";
}

namespace LendingService.Kafka;

public sealed class ReservationCancelledEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "reservation-cancelled";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int UserId { get; init; }
    public int BookId { get; init; }
    public DateTime ReservationDate { get; init; }
    public string Status { get; init; } = "Cancelled";
}

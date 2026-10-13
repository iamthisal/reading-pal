namespace LendingService.Kafka;

public sealed class ReservationAcceptedEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "reservation-accepted";
    public int SchemaVersion { get; init; } = 2;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int UserId { get; init; }
    public int BookId { get; init; }
    public DateTime ReservationDate { get; init; }
    public string Status { get; init; } = "Borrowed";
    // Added in schema version 2 so consumers (Notification Service) can show the loan period.
    public DateTime CheckoutDate { get; init; }
    public DateTime DueDate { get; init; }
}

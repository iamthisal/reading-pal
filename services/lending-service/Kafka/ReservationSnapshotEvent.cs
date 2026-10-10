namespace LendingService.Kafka;

/// <summary>
/// The current state of one active reservation, published once by <see cref="ReservationSnapshotService"/>
/// so consumers that started after the reservation was made (or lost its earlier events) can catch up.
/// Notifies no one by itself.
/// </summary>
public sealed class ReservationSnapshotEvent
{
    public Guid EventId { get; init; } = Guid.NewGuid();
    public string EventType { get; init; } = "reservation-snapshot";
    public int SchemaVersion { get; init; } = 1;
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
    public int ReservationId { get; init; }
    public int UserId { get; init; }
    public int BookId { get; init; }
    // Pending, Accepting, Borrowed or Returning.
    public string Status { get; init; } = string.Empty;
    public DateTime ReservationDate { get; init; }
}

namespace LendingService.Models;

public sealed class ReservationEventOutbox
{
    public Guid Id { get; set; }
    public int ReservationId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? PublishedAtUtc { get; set; }
}

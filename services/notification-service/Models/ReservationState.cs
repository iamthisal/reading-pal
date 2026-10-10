namespace NotificationService.Models;

/// <summary>
/// The Notification Service's own record of each reservation, built from Lending's events, so it knows
/// which customers have a pending reservation or active borrowing for a book when that book is deleted.
/// </summary>
public sealed class ReservationState
{
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public string Status { get; set; } = ReservationStatuses.Pending;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class ReservationStatuses
{
    public const string Pending = "Pending";
    public const string Borrowed = "Borrowed";
    // Cancelled or returned: no longer connected to the book.
    public const string Closed = "Closed";

    // Status only moves forward. Events for one reservation arrive on different topics, so Kafka does not
    // guarantee their order; a late reservation-created must not reopen a reservation already closed.
    public static int Rank(string status) => status switch
    {
        Pending => 0,
        Borrowed => 1,
        Closed => 2,
        _ => -1
    };
}

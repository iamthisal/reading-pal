namespace NotificationService.Models;

/// <summary>
/// A book that has been removed from the catalogue. Kept so that a reservation whose event reaches this
/// service after the deletion (the two travel on different Kafka topics, so the order is not guaranteed)
/// can still be told its book is gone.
/// </summary>
public sealed class DeletedBook
{
    public int BookId { get; set; }
    /// <summary>The book-deleted event, reused as the notification's event ID so nobody is told twice.</summary>
    public Guid EventId { get; set; }
    public string Title { get; set; } = string.Empty;
    public DateTime DeletedAtUtc { get; set; } = DateTime.UtcNow;
}

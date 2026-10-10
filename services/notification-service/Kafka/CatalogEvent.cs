namespace NotificationService.Kafka;

/// <summary>Shape of the book events Inventory publishes (book-created, book-deleted).</summary>
public sealed class CatalogEvent
{
    public Guid EventId { get; init; }
    public string? EventType { get; init; }
    public DateTime TimestampUtc { get; init; }
    public int BookId { get; init; }
    public CatalogBook? Book { get; init; }
}

public sealed class CatalogBook
{
    public string? Title { get; init; }
    public string? Author { get; init; }
}

public static class CatalogEventTypes
{
    public const string BookCreated = "book-created";
    public const string BookDeleted = "book-deleted";
}

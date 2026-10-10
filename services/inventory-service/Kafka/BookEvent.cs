using InventoryService.DTOs;

namespace InventoryService.Kafka
{
    public sealed class BookEvent
    {
        public Guid EventId { get; init; } = Guid.NewGuid();
        public string EventType { get; init; } = string.Empty;
        public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
        public int BookId { get; init; }
        public BookResponse Book { get; init; } = new();
        // What the admin did: created, updated, marked-unavailable, marked-available or deleted.
        public string Action { get; init; } = string.Empty;
        // The acting admin's token subject and email, so other admins can be told and the actor left out.
        public string? PerformedBy { get; init; }
        public string? PerformedByEmail { get; init; }
    }
}
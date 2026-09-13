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
    }
}
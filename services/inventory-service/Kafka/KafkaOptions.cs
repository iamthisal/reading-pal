namespace InventoryService.Kafka
{
    public sealed class KafkaOptions
    {
        public string BootstrapServers { get; set; } = string.Empty;
        public string SecurityProtocol { get; set; } = "Plaintext";
        public KafkaTopicOptions Topics { get; set; } = new();
    }

    public sealed class KafkaTopicOptions
    {
        public string BookCreated { get; set; } = "book-created";
        public string BookUpdated { get; set; } = "book-updated";
        public string BookDeleted { get; set; } = "book-deleted";
    }
}
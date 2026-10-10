namespace NotificationService.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string SecurityProtocol { get; set; } = "Plaintext";
    // Kafka remembers this group's position, so a restart resumes where it stopped.
    public string GroupId { get; set; } = "notification-service";
    public KafkaTopicOptions Topics { get; set; } = new();
}

public sealed class KafkaTopicOptions
{
    public string ReservationCreated { get; set; } = "reservation-created";
    public string ReservationAccepted { get; set; } = "reservation-accepted";
    public string ReservationCancelled { get; set; } = "reservation-cancelled";
    public string BookReturned { get; set; } = "book-returned";
    public string FineRecorded { get; set; } = "fine-recorded";
    public string BookDueSoon { get; set; } = "book-due-soon";
    public string ReservationSnapshot { get; set; } = "reservation-snapshot";
    // Published by Inventory.
    public string BookCreated { get; set; } = "book-created";
    public string BookDeleted { get; set; } = "book-deleted";
}

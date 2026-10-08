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
    public string ReservationAccepted { get; set; } = "reservation-accepted";
    public string ReservationCancelled { get; set; } = "reservation-cancelled";
    public string BookReturned { get; set; } = "book-returned";
}

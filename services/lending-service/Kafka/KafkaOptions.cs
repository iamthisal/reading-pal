namespace LendingService.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string SecurityProtocol { get; set; } = "Plaintext";
    public KafkaTopicOptions Topics { get; set; } = new();
}

public sealed class KafkaTopicOptions
{
    public string BookReturned { get; set; } = "book-returned";
    public string ReservationAccepted { get; set; } = "reservation-accepted";
    public string ReservationCancelled { get; set; } = "reservation-cancelled";
    public string ReservationCreated { get; set; } = "reservation-created";
    public string FineRecorded { get; set; } = "fine-recorded";
    public string BookDueSoon { get; set; } = "book-due-soon";
}

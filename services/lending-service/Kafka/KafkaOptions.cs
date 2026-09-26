namespace LendingService.Kafka;

public sealed class KafkaOptions
{
    public string BootstrapServers { get; set; } = "localhost:9092";
    public string SecurityProtocol { get; set; } = "Plaintext";
    public KafkaTopicOptions Topics { get; set; } = new();
}

public sealed class KafkaTopicOptions
{
    public string ReservationAccepted { get; set; } = "reservation-accepted";
}

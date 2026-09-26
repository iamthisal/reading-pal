using Confluent.Kafka;
using System.Text.Json;
using LendingService.Models;
using Microsoft.Extensions.Options;

namespace LendingService.Kafka;

public sealed class KafkaReservationEventPublisher(IProducer<string, string> producer, IOptions<KafkaOptions> options)
    : IReservationEventPublisher
{
    public async Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken)
    {
        using var payload = JsonDocument.Parse(message.Payload);
        var eventType = payload.RootElement.GetProperty("eventType").GetString();
        var topic = eventType switch
        {
            "reservation-accepted" => options.Value.Topics.ReservationAccepted,
            "reservation-cancelled" => options.Value.Topics.ReservationCancelled,
            _ => throw new InvalidOperationException($"Unsupported reservation event type '{eventType}'.")
        };
        await producer.ProduceAsync(topic, new Message<string, string>
        {
            Key = message.ReservationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Value = message.Payload
        }, cancellationToken);
    }
}

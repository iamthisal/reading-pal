using Confluent.Kafka;
using LendingService.Models;
using Microsoft.Extensions.Options;

namespace LendingService.Kafka;

public sealed class KafkaReservationEventPublisher(IProducer<string, string> producer, IOptions<KafkaOptions> options)
    : IReservationEventPublisher
{
    public async Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken)
    {
        await producer.ProduceAsync(options.Value.Topics.ReservationAccepted, new Message<string, string>
        {
            Key = message.ReservationId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Value = message.Payload
        }, cancellationToken);
    }
}

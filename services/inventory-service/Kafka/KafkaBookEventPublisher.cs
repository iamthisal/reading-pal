using System.Text.Json;
using Confluent.Kafka;
using Microsoft.Extensions.Options;

namespace InventoryService.Kafka
{
    public sealed class KafkaBookEventPublisher : IBookEventPublisher
    {
        private readonly IProducer<string, string> _producer;
        private readonly KafkaOptions _options;
        private readonly JsonSerializerOptions _jsonOptions = new(JsonSerializerDefaults.Web);

        public KafkaBookEventPublisher(IProducer<string, string> producer, IOptions<KafkaOptions> options)
        {
            _producer = producer;
            _options = options.Value;
        }

        public async Task PublishAsync(BookEvent bookEvent, CancellationToken cancellationToken = default)
        {
            var topic = bookEvent.EventType switch
            {
                "book-created" => _options.Topics.BookCreated,
                "book-updated" => _options.Topics.BookUpdated,
                "book-deleted" => _options.Topics.BookDeleted,
                _ => throw new ArgumentException($"Unsupported book event type '{bookEvent.EventType}'.", nameof(bookEvent))
            };

            var message = new Message<string, string>
            {
                Key = bookEvent.BookId.ToString(),
                Value = JsonSerializer.Serialize(bookEvent, _jsonOptions)
            };

            await _producer.ProduceAsync(topic, message, cancellationToken);
        }
    }
}
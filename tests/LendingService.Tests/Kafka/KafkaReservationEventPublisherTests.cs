using Confluent.Kafka;
using LendingService.Kafka;
using LendingService.Models;
using Microsoft.Extensions.Options;
using Moq;

namespace LendingService.Tests.Kafka;

public class KafkaReservationEventPublisherTests
{
    [Theory]
    [InlineData("reservation-accepted", "accepted-topic")]
    [InlineData("reservation-cancelled", "cancelled-topic")]
    [InlineData("book-returned", "returned-topic")]
    [InlineData("reservation-created", "created-topic")]
    [InlineData("fine-recorded", "fine-topic")]
    [InlineData("book-due-soon", "due-soon-topic")]
    [InlineData("reservation-snapshot", "snapshot-topic")]
    public async Task PublishAsync_RoutesSupportedEventToConfiguredTopic(string eventType, string expectedTopic)
    {
        string? topic = null;
        Message<string, string>? produced = null;
        var producer = new Mock<IProducer<string, string>>();
        producer.Setup(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()))
            .Callback<string, Message<string, string>, CancellationToken>((value, message, _) =>
            {
                topic = value;
                produced = message;
            })
            .ReturnsAsync(new DeliveryResult<string, string>());
        var options = Options.Create(new KafkaOptions
        {
            Topics = new KafkaTopicOptions
            {
                ReservationAccepted = "accepted-topic",
                ReservationCancelled = "cancelled-topic",
                BookReturned = "returned-topic",
                ReservationCreated = "created-topic",
                FineRecorded = "fine-topic",
                BookDueSoon = "due-soon-topic",
                ReservationSnapshot = "snapshot-topic"
            }
        });
        var publisher = new KafkaReservationEventPublisher(producer.Object, options);
        var outbox = new ReservationEventOutbox
        {
            ReservationId = 42,
            EventType = eventType,
            Payload = $"{{\"eventType\":\"{eventType}\"}}"
        };

        await publisher.PublishAsync(outbox, CancellationToken.None);

        Assert.Equal(expectedTopic, topic);
        Assert.Equal("42", produced!.Key);
        Assert.Equal(outbox.Payload, produced.Value);
    }

    [Fact]
    public async Task PublishAsync_WithUnsupportedEventType_ThrowsWithoutProducing()
    {
        var producer = new Mock<IProducer<string, string>>();
        var publisher = new KafkaReservationEventPublisher(producer.Object, Options.Create(new KafkaOptions()));
        var outbox = new ReservationEventOutbox { ReservationId = 1, Payload = "{\"eventType\":\"unknown\"}" };

        await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(outbox, CancellationToken.None));
        producer.Verify(p => p.ProduceAsync(It.IsAny<string>(), It.IsAny<Message<string, string>>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}

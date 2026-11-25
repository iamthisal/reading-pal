using Confluent.Kafka;
using LendingService.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace LendingService.UnitTests;

public class KafkaReservationEventPublisherTests
{
    [Fact]
    // Proves unknown lending events fail before topic configuration is accessed.
    public async Task PublishAsync_UnsupportedEvent_ThrowsBeforeReadingTopics()
    {
        // Arrange
        var producer = new Mock<IProducer<string, string>>();
        var options = new Mock<IOptions<KafkaOptions>>();
        var publisher = new KafkaReservationEventPublisher(producer.Object, options.Object);
        var message = LendingTestData.Message();
        message.Payload = "{\"eventType\":\"unsupported\"}";
        // Act
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => publisher.PublishAsync(message, CancellationToken.None));
        // Assert
        Assert.Equal("Unsupported reservation event type 'unsupported'.", exception.Message);
        options.Verify(settings => settings.Value, Times.Never);
    }
}

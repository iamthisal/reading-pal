using Confluent.Kafka;
using InventoryService.Kafka;
using Microsoft.Extensions.Options;
using Moq;

namespace InventoryService.UnitTests;

public class KafkaBookEventPublisherTests
{
    [Fact]
    // Proves unknown event types fail explicitly and publisher options are read once.
    public async Task PublishAsync_UnsupportedEvent_ThrowsArgumentException()
    {
        // Arrange
        var producer = new Mock<IProducer<string, string>>();
        var options = new Mock<IOptions<KafkaOptions>>();
        options.Setup(settings => settings.Value).Returns(new KafkaOptions());
        var publisher = new KafkaBookEventPublisher(producer.Object, options.Object);
        var bookEvent = new BookEvent { EventType = "unsupported", BookId = 1 };
        // Act
        var exception = await Assert.ThrowsAsync<ArgumentException>(() => publisher.PublishAsync(bookEvent));
        // Assert
        Assert.Equal("bookEvent", exception.ParamName);
        options.Verify(settings => settings.Value, Times.Once);
    }
}

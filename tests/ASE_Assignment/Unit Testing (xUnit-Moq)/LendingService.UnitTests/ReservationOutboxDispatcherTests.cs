using LendingService.Kafka;
using Moq;

namespace LendingService.UnitTests;

public class ReservationOutboxDispatcherTests
{
    [Fact]
    // Proves a pending message is published exactly once and marked as published.
    public async Task DispatchAsync_PendingMessage_PublishesOnce()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var message = LendingTestData.Message();
        db.ReservationEvents.Add(message);
        await db.SaveChangesAsync();
        var publisher = new Mock<IReservationEventPublisher>();
        publisher.Setup(service => service.PublishAsync(message, CancellationToken.None)).Returns(Task.CompletedTask);
        var dispatcher = new ReservationOutboxDispatcher(db, publisher.Object);
        // Act
        await dispatcher.DispatchAsync(CancellationToken.None);
        // Assert
        Assert.True(message.PublishedAtUtc.HasValue);
        publisher.Verify(service => service.PublishAsync(message, CancellationToken.None), Times.Once);
    }

    [Fact]
    // Proves a previously published message is never sent again.
    public async Task DispatchAsync_PublishedMessage_DoesNotPublish()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var message = LendingTestData.Message();
        message.PublishedAtUtc = LendingTestData.Now;
        db.ReservationEvents.Add(message);
        await db.SaveChangesAsync();
        var publisher = new Mock<IReservationEventPublisher>();
        var dispatcher = new ReservationOutboxDispatcher(db, publisher.Object);
        // Act
        await dispatcher.DispatchAsync(CancellationToken.None);
        // Assert
        Assert.Equal<DateTime?>(LendingTestData.Now, message.PublishedAtUtc);
        publisher.Verify(service => service.PublishAsync(message, CancellationToken.None), Times.Never);
    }

    [Fact]
    // Proves an unavailable publisher leaves the message pending for retry.
    public async Task DispatchAsync_PublisherUnavailable_KeepsMessagePending()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var message = LendingTestData.Message();
        db.ReservationEvents.Add(message);
        await db.SaveChangesAsync();
        var publisher = new Mock<IReservationEventPublisher>();
        publisher.Setup(service => service.PublishAsync(message, CancellationToken.None))
            .Returns(Task.FromException(new InvalidOperationException("Publisher unavailable")));
        var dispatcher = new ReservationOutboxDispatcher(db, publisher.Object);
        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => dispatcher.DispatchAsync(CancellationToken.None));
        // Assert
        Assert.False(message.PublishedAtUtc.HasValue);
        publisher.Verify(service => service.PublishAsync(message, CancellationToken.None), Times.Once);
    }
}

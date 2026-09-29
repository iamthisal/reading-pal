using LendingService.Kafka;
using LendingService.Models;
using LendingService.Tests.TestSupport;

namespace LendingService.Tests.Kafka;

public class ReservationOutboxDispatcherTests
{
    [Fact]
    public async Task DispatchAsync_PublishesPendingMessagesInOrderAndMarksThemPublished()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var first = new ReservationEventOutbox { Id = Guid.NewGuid(), ReservationId = 1,
            EventType = "reservation-accepted", Payload = "{}", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-2) };
        var second = new ReservationEventOutbox { Id = Guid.NewGuid(), ReservationId = 2,
            EventType = "book-returned", Payload = "{}", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-1) };
        var alreadyPublished = new ReservationEventOutbox { Id = Guid.NewGuid(), ReservationId = 3,
            EventType = "reservation-cancelled", Payload = "{}", CreatedAtUtc = DateTime.UtcNow.AddMinutes(-3), PublishedAtUtc = DateTime.UtcNow };
        context.ReservationEvents.AddRange(second, alreadyPublished, first);
        await context.SaveChangesAsync();
        var publisher = new RecordingPublisher();

        await new ReservationOutboxDispatcher(context, publisher).DispatchAsync(CancellationToken.None);

        Assert.Equal(new[] { first.Id, second.Id }, publisher.Messages.Select(m => m.Id));
        Assert.NotNull(first.PublishedAtUtc);
        Assert.NotNull(second.PublishedAtUtc);
        Assert.NotNull(alreadyPublished.PublishedAtUtc);
    }

    [Fact]
    public async Task DispatchAsync_WhenPublishingFails_LeavesMessageUnpublishedForRetry()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var message = new ReservationEventOutbox { Id = Guid.NewGuid(), ReservationId = 1,
            EventType = "book-returned", Payload = "{}", CreatedAtUtc = DateTime.UtcNow };
        context.ReservationEvents.Add(message);
        await context.SaveChangesAsync();
        var publisher = new ThrowingPublisher();

        await Assert.ThrowsAsync<HttpRequestException>(() =>
            new ReservationOutboxDispatcher(context, publisher).DispatchAsync(CancellationToken.None));

        Assert.Null(message.PublishedAtUtc);
    }

    private sealed class RecordingPublisher : IReservationEventPublisher
    {
        public List<ReservationEventOutbox> Messages { get; } = new();

        public Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }

    private sealed class ThrowingPublisher : IReservationEventPublisher
    {
        public Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken) =>
            throw new HttpRequestException("Kafka unavailable");
    }
}

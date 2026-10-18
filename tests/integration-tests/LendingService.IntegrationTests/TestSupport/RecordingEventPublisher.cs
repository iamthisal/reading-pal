using System.Collections.Concurrent;
using LendingService.Kafka;
using LendingService.Models;

namespace LendingService.IntegrationTests.TestSupport;

/// <summary>
/// Replaces <see cref="KafkaReservationEventPublisher"/> for the whole test host. The real
/// <see cref="LendingService.Kafka.ReservationOutboxWorker"/> background service keeps running during
/// these tests (it is part of the real app), so this records what it would have published to Kafka
/// without ever opening a socket, and lets tests assert an event was queued and drained.
/// </summary>
public sealed class RecordingEventPublisher : IReservationEventPublisher
{
    private readonly ConcurrentQueue<ReservationEventOutbox> _messages = new();

    public IReadOnlyCollection<ReservationEventOutbox> Messages => _messages.ToArray();

    public Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken)
    {
        _messages.Enqueue(message);
        return Task.CompletedTask;
    }
}

using LendingService.Models;

namespace LendingService.Kafka;

public interface IReservationEventPublisher
{
    Task PublishAsync(ReservationEventOutbox message, CancellationToken cancellationToken);
}

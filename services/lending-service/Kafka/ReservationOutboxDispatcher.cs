using LendingService.Data;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Kafka;

public sealed class ReservationOutboxDispatcher(LendingDbContext db, IReservationEventPublisher publisher)
{
    public async Task DispatchAsync(CancellationToken cancellationToken)
    {
        var messages = await db.ReservationEvents.Where(e => e.PublishedAtUtc == null)
            .OrderBy(e => e.CreatedAtUtc).ThenBy(e => e.Id).Take(20).ToListAsync(cancellationToken);
        foreach (var message in messages)
        {
            await publisher.PublishAsync(message, cancellationToken);
            message.PublishedAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
    }
}

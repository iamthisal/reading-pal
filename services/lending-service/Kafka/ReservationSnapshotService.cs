using System.Text.Json;
using LendingService.Data;
using LendingService.Models;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Kafka;

/// <summary>
/// Queues one reservation-snapshot event for every active reservation (pending or borrowed, including
/// those mid-accept or mid-return) that has not had one yet. This backfills consumers such as the
/// Notification Service with reservations whose earlier events they never saw: ones made before
/// reservation-created existed, or whose events expired from the broker.
/// The outbox's unique (ReservationId, EventType) index keeps it to one snapshot per reservation,
/// even across restarts and concurrent instances.
/// </summary>
public sealed class ReservationSnapshotService(LendingDbContext db, ILogger<ReservationSnapshotService> logger)
{
    public const string EventType = "reservation-snapshot";

    public async Task<int> QueueSnapshotsAsync(CancellationToken cancellationToken)
    {
        var reservations = await db.Reservations.AsNoTracking()
            // Explicit comparisons: array.Contains binds to a span overload under C# 14 that EF Core 8 cannot translate.
            .Where(r => (r.Status == "Pending" || r.Status == "Accepting" || r.Status == "Borrowed" || r.Status == "Returning")
                && !db.ReservationEvents.Any(e => e.ReservationId == r.Id && e.EventType == EventType))
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

        var queued = 0;
        foreach (var reservation in reservations)
        {
            var snapshot = new ReservationSnapshotEvent
            {
                ReservationId = reservation.Id,
                UserId = reservation.UserId,
                BookId = reservation.BookId,
                Status = reservation.Status,
                ReservationDate = DateTime.SpecifyKind(reservation.ReservationDate, DateTimeKind.Utc)
            };
            db.ReservationEvents.Add(new ReservationEventOutbox
            {
                Id = snapshot.EventId,
                ReservationId = reservation.Id,
                EventType = EventType,
                CreatedAtUtc = snapshot.TimestampUtc,
                Payload = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions(JsonSerializerDefaults.Web))
            });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
                queued++;
            }
            catch (DbUpdateException)
            {
                db.ChangeTracker.Clear();
                // Another instance queued this snapshot first; anything else is a real failure.
                if (!await db.ReservationEvents.AnyAsync(e => e.ReservationId == reservation.Id && e.EventType == EventType, cancellationToken))
                    throw;
                logger.LogInformation("Reservation {ReservationId} already has a snapshot.", reservation.Id);
            }
        }
        return queued;
    }
}

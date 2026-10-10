using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Kafka;
using NotificationService.Models;

namespace NotificationService.Services;

public enum HandleOutcome
{
    Created,
    Duplicate,
    Skipped
}

/// <summary>
/// Turns one Kafka message into at most one notification per audience (customer, admins). Safe to call repeatedly with the same
/// message: Kafka delivers at least once, so the event ID decides whether a notification already exists.
/// Database failures are thrown so the consumer can retry the message instead of losing it.
/// </summary>
public sealed class LendingEventHandler(NotificationDbContext db, IBookTitleLookup titles, ILogger<LendingEventHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HandleOutcome> HandleAsync(string? payload, CancellationToken cancellationToken)
    {
        LendingEvent? evt;
        try
        {
            evt = string.IsNullOrWhiteSpace(payload) ? null : JsonSerializer.Deserialize<LendingEvent>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Skipping a Kafka message that is not valid event JSON.");
            return HandleOutcome.Skipped;
        }

        if (evt == null || evt.EventId == Guid.Empty || evt.UserId <= 0 || evt.BookId <= 0)
        {
            logger.LogWarning("Skipping a Kafka message without a usable event ID, user or book.");
            return HandleOutcome.Skipped;
        }

        await TrackReservationStateAsync(evt, cancellationToken);
        // A snapshot only updates reservation state; it notifies no one.
        if (evt.EventType == LendingEventTypes.ReservationSnapshot) return HandleOutcome.Skipped;

        // One event can notify the customer, the admins, or both. Work out which audiences it applies to
        // before asking Inventory for the title, so duplicates and irrelevant events cost no HTTP call.
        var audiences = new List<string>();
        if (NotificationFactory.CreateForUser(evt, null) != null) audiences.Add(NotificationAudiences.User);
        if (NotificationFactory.CreateForAdmin(evt, null) != null) audiences.Add(NotificationAudiences.Admin);
        if (audiences.Count == 0)
        {
            logger.LogWarning("Skipping unsupported event type {EventType} ({EventId}).", evt.EventType, evt.EventId);
            return HandleOutcome.Skipped;
        }

        var stored = await StoredAudiencesAsync(evt.EventId, cancellationToken);
        var missing = audiences.Except(stored).ToList();
        if (missing.Count == 0) return HandleOutcome.Duplicate;

        var title = await titles.GetTitleAsync(evt.BookId, cancellationToken);
        foreach (var audience in missing)
        {
            db.Notifications.Add(audience == NotificationAudiences.User
                ? NotificationFactory.CreateForUser(evt, title)!
                : NotificationFactory.CreateForAdmin(evt, title)!);
        }
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another delivery of the same event may have won the unique (EventId, Audience) index.
            db.ChangeTracker.Clear();
            var nowStored = await StoredAudiencesAsync(evt.EventId, cancellationToken);
            if (audiences.All(nowStored.Contains)) return HandleOutcome.Duplicate;
            throw;
        }
        return HandleOutcome.Created;
    }

    /// <summary>
    /// Keeps ReservationStates in step with Lending so a deleted book can be matched to the customers
    /// who still have a pending reservation or active borrowing for it. Status only moves forward.
    /// </summary>
    private async Task TrackReservationStateAsync(LendingEvent evt, CancellationToken cancellationToken)
    {
        var status = evt.EventType switch
        {
            LendingEventTypes.ReservationCreated => ReservationStatuses.Pending,
            LendingEventTypes.ReservationAccepted => ReservationStatuses.Borrowed,
            LendingEventTypes.ReservationCancelled or LendingEventTypes.BookReturned => ReservationStatuses.Closed,
            // Backfill from Lending's current state. The forward-only rule below means an older snapshot
            // can never reopen a reservation that a newer event already closed.
            LendingEventTypes.ReservationSnapshot => evt.Status switch
            {
                "Pending" or "Accepting" => ReservationStatuses.Pending,
                "Borrowed" or "Returning" => ReservationStatuses.Borrowed,
                _ => null
            },
            _ => null
        };
        if (status == null || evt.ReservationId <= 0) return;

        // Two attempts: if another delivery inserted the same reservation between our read and our save,
        // reload it and apply this transition to the stored row instead of discarding it.
        for (var attempt = 1; ; attempt++)
        {
            var state = await db.ReservationStates.SingleOrDefaultAsync(s => s.ReservationId == evt.ReservationId, cancellationToken);
            if (state == null)
            {
                db.ReservationStates.Add(new ReservationState
                {
                    ReservationId = evt.ReservationId, UserId = evt.UserId, BookId = evt.BookId, Status = status
                });
            }
            else if (ReservationStatuses.Rank(status) > ReservationStatuses.Rank(state.Status))
            {
                state.Status = status;
                state.UpdatedAtUtc = DateTime.UtcNow;
            }
            else
            {
                return;
            }

            try
            {
                await db.SaveChangesAsync(cancellationToken);
                return;
            }
            catch (DbUpdateException) when (attempt == 1)
            {
                db.ChangeTracker.Clear();
                // Only a duplicate insert is recoverable here. Anything else (or a second failure) propagates,
                // so the offset is not committed and Kafka redelivers the message.
                if (!await db.ReservationStates.AsNoTracking().AnyAsync(s => s.ReservationId == evt.ReservationId, cancellationToken))
                    throw;
            }
        }
    }

    private Task<List<string>> StoredAudiencesAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Notifications.AsNoTracking().Where(n => n.EventId == eventId).Select(n => n.Audience).ToListAsync(cancellationToken);
}

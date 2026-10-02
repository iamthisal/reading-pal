using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Kafka;

namespace NotificationService.Services;

public enum HandleOutcome
{
    Created,
    Duplicate,
    Skipped
}

/// <summary>
/// Turns one Kafka message into at most one notification. Safe to call repeatedly with the same
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

        if (await db.Notifications.AsNoTracking().AnyAsync(n => n.EventId == evt.EventId, cancellationToken))
            return HandleOutcome.Duplicate;

        var title = await titles.GetTitleAsync(evt.BookId, cancellationToken);
        var notification = NotificationFactory.Create(evt, title);
        if (notification == null)
        {
            logger.LogWarning("Skipping unsupported event type {EventType} ({EventId}).", evt.EventType, evt.EventId);
            return HandleOutcome.Skipped;
        }

        db.Notifications.Add(notification);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Another delivery of the same event may have won the unique EventId index.
            if (await IsAlreadyStoredAsync(evt.EventId, cancellationToken)) return HandleOutcome.Duplicate;
            throw;
        }
        return HandleOutcome.Created;
    }

    private async Task<bool> IsAlreadyStoredAsync(Guid eventId, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        return await db.Notifications.AsNoTracking().AnyAsync(n => n.EventId == eventId, cancellationToken);
    }
}

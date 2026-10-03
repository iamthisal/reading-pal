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

    private Task<List<string>> StoredAudiencesAsync(Guid eventId, CancellationToken cancellationToken) =>
        db.Notifications.AsNoTracking().Where(n => n.EventId == eventId).Select(n => n.Audience).ToListAsync(cancellationToken);
}

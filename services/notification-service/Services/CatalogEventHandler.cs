using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Kafka;
using NotificationService.Models;

namespace NotificationService.Services;

/// <summary>
/// Turns Inventory's catalogue events into notifications:
/// <list type="bullet">
/// <item>book-created: one shared announcement for customers registered and active at that time.</item>
/// <item>book-deleted: one notification for each customer with a pending reservation or active borrowing
/// for that book. The book's title is saved from the event, because the book no longer exists.</item>
/// </list>
/// Idempotent per (EventId, Audience, UserId), like <see cref="LendingEventHandler"/>.
/// </summary>
public sealed class CatalogEventHandler(NotificationDbContext db, ILogger<CatalogEventHandler> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<HandleOutcome> HandleAsync(string? payload, CancellationToken cancellationToken)
    {
        CatalogEvent? evt;
        try
        {
            evt = string.IsNullOrWhiteSpace(payload) ? null : JsonSerializer.Deserialize<CatalogEvent>(payload, JsonOptions);
        }
        catch (JsonException ex)
        {
            logger.LogWarning(ex, "Skipping a catalogue message that is not valid event JSON.");
            return HandleOutcome.Skipped;
        }
        if (evt == null || evt.EventId == Guid.Empty || evt.BookId <= 0)
        {
            logger.LogWarning("Skipping a catalogue message without a usable event ID or book.");
            return HandleOutcome.Skipped;
        }

        return evt.EventType switch
        {
            CatalogEventTypes.BookCreated => await AnnounceAsync(evt, cancellationToken),
            CatalogEventTypes.BookDeleted => await NotifyAffectedCustomersAsync(evt, cancellationToken),
            _ => HandleOutcome.Skipped
        };
    }

    private async Task<HandleOutcome> AnnounceAsync(CatalogEvent evt, CancellationToken cancellationToken)
    {
        if (await db.Notifications.AnyAsync(n => n.EventId == evt.EventId && n.Audience == NotificationAudiences.Customers, cancellationToken))
            return HandleOutcome.Duplicate;

        db.Notifications.Add(NotificationFactory.CreateNewBookAnnouncement(evt));
        return await SaveAsync(evt.EventId, cancellationToken);
    }

    private async Task<HandleOutcome> NotifyAffectedCustomersAsync(CatalogEvent evt, CancellationToken cancellationToken)
    {
        var affected = await db.ReservationStates.AsNoTracking()
            .Where(s => s.BookId == evt.BookId && (s.Status == ReservationStatuses.Pending || s.Status == ReservationStatuses.Borrowed))
            .Select(s => s.UserId).Distinct().ToListAsync(cancellationToken);
        if (affected.Count == 0)
        {
            logger.LogInformation("Book {BookId} was deleted; no customer had a pending reservation or active borrowing for it.", evt.BookId);
            return HandleOutcome.Skipped;
        }

        var alreadyNotified = await db.Notifications.AsNoTracking()
            .Where(n => n.EventId == evt.EventId && n.Audience == NotificationAudiences.User)
            .Select(n => n.UserId).ToListAsync(cancellationToken);
        var missing = affected.Except(alreadyNotified).ToList();
        if (missing.Count == 0) return HandleOutcome.Duplicate;

        foreach (var userId in missing)
            db.Notifications.Add(NotificationFactory.CreateBookDeletedNotice(evt, userId));
        return await SaveAsync(evt.EventId, cancellationToken);
    }

    private async Task<HandleOutcome> SaveAsync(Guid eventId, CancellationToken cancellationToken)
    {
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return HandleOutcome.Created;
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery of the same event stored it first.
            db.ChangeTracker.Clear();
            if (await db.Notifications.AnyAsync(n => n.EventId == eventId, cancellationToken)) return HandleOutcome.Duplicate;
            throw;
        }
    }
}

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
/// <item>book-created, book-updated and book-deleted: one admin notification, which every admin except the
/// one who made the change sees.</item>
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

        // Everything this event should produce; anything already stored (a redelivery) is skipped below.
        var wanted = new List<Notification>();
        if (NotificationFactory.CreateAdminCatalogNotice(evt) is { } adminNotice) wanted.Add(adminNotice);
        if (evt.EventType == CatalogEventTypes.BookCreated) wanted.Add(NotificationFactory.CreateNewBookAnnouncement(evt));
        if (evt.EventType == CatalogEventTypes.BookDeleted)
        {
            var affected = await db.ReservationStates.AsNoTracking()
                .Where(s => s.BookId == evt.BookId && (s.Status == ReservationStatuses.Pending || s.Status == ReservationStatuses.Borrowed))
                .Select(s => s.UserId).Distinct().ToListAsync(cancellationToken);
            if (affected.Count == 0)
                logger.LogInformation("Book {BookId} was deleted; no customer had a pending reservation or active borrowing for it.", evt.BookId);
            wanted.AddRange(affected.Select(userId => NotificationFactory.CreateBookDeletedNotice(evt, userId)));
        }
        if (wanted.Count == 0) return HandleOutcome.Skipped;

        var stored = await db.Notifications.AsNoTracking()
            .Where(n => n.EventId == evt.EventId)
            .Select(n => new { n.Audience, n.UserId }).ToListAsync(cancellationToken);
        var missing = wanted.Where(w => !stored.Any(s => s.Audience == w.Audience && s.UserId == w.UserId)).ToList();
        if (missing.Count == 0) return HandleOutcome.Duplicate;

        db.Notifications.AddRange(missing);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return HandleOutcome.Created;
        }
        catch (DbUpdateException)
        {
            // A concurrent delivery of the same event stored them first; anything else is a real failure.
            db.ChangeTracker.Clear();
            var nowStored = await db.Notifications.AsNoTracking()
                .Where(n => n.EventId == evt.EventId)
                .Select(n => new { n.Audience, n.UserId }).ToListAsync(cancellationToken);
            if (wanted.All(w => nowStored.Any(s => s.Audience == w.Audience && s.UserId == w.UserId))) return HandleOutcome.Duplicate;
            throw;
        }
    }
}

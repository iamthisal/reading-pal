using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Models;

namespace NotificationService.Services;

/// <summary>Stores per-recipient read records for shared notifications.</summary>
public sealed class NotificationReadStore(NotificationDbContext db)
{
    /// <summary>
    /// Marks the given notifications read for one recipient and returns how many were newly marked.
    /// Repeating it is safe. It only reports success once every read record exists: if a parallel request
    /// (another tab) stored them first that is fine, but any other database failure propagates.
    /// </summary>
    public async Task<int> MarkReadAsync(string recipientKey, IReadOnlyCollection<int> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0) return 0;
        var alreadyRead = await db.NotificationReads.AsNoTracking()
            .Where(r => r.RecipientKey == recipientKey && notificationIds.Contains(r.NotificationId))
            .Select(r => r.NotificationId).ToListAsync(cancellationToken);
        var toAdd = notificationIds.Except(alreadyRead).ToList();
        if (toAdd.Count == 0) return 0;
        foreach (var id in toAdd)
            db.NotificationReads.Add(new NotificationRead { NotificationId = id, RecipientKey = recipientKey });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            var stored = await db.NotificationReads.AsNoTracking()
                .Where(r => r.RecipientKey == recipientKey && toAdd.Contains(r.NotificationId))
                .Select(r => r.NotificationId).ToListAsync(cancellationToken);
            if (toAdd.Except(stored).Any()) throw;
        }
        return toAdd.Count;
    }
}

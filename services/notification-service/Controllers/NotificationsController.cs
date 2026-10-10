using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;

namespace NotificationService.Controllers;

/// <summary>
/// A customer's notifications: their own (Audience = User) plus catalogue announcements (Audience =
/// Customers) made after their account became active. Announcements are shared rows, so their read
/// state is kept per customer in NotificationReads.
/// </summary>
[ApiController]
[Route("api/notifications")]
[Authorize(Roles = "User")]
public sealed class NotificationsController(NotificationDbContext db) : ControllerBase
{
    private const int MaxPageSize = 50;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<NotificationResponse>>> Get(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);
        // Enough of each source to fill the requested page after merging.
        var take = page * pageSize;

        var personalQuery = Personal(userId);
        if (unreadOnly) personalQuery = personalQuery.Where(n => !n.IsRead);
        var personal = await personalQuery
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Take(take).ToListAsync(cancellationToken);

        var items = personal.Select(n => ToResponse(n, n.IsRead)).ToList();
        if (TryGetActiveSince(out var activeSince))
        {
            var announcementQuery = Announcements(activeSince);
            if (unreadOnly) announcementQuery = announcementQuery.Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId));
            var announcements = await announcementQuery
                .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
                .Take(take)
                .Select(n => new { Notification = n, IsRead = db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId) })
                .ToListAsync(cancellationToken);
            items.AddRange(announcements.Select(a => ToResponse(a.Notification, a.IsRead)));
        }

        return Ok(items
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToList());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var count = await Personal(userId).CountAsync(n => !n.IsRead, cancellationToken);
        if (TryGetActiveSince(out var activeSince))
            count += await Announcements(activeSince)
                .CountAsync(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId), cancellationToken);
        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });

        // Another customer's notification, or an announcement this customer cannot see, is reported as missing.
        var notification = await Personal(userId).AsTracking().SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (notification != null)
        {
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                notification.ReadAtUtc = DateTime.UtcNow;
                await db.SaveChangesAsync(cancellationToken);
            }
            return Ok(ToResponse(notification, true));
        }

        if (!TryGetActiveSince(out var activeSince)) return NotFound(new { message = "Notification not found." });
        var announcement = await Announcements(activeSince).SingleOrDefaultAsync(n => n.Id == id, cancellationToken);
        if (announcement == null) return NotFound(new { message = "Notification not found." });
        await MarkAnnouncementsReadAsync(userId, new[] { announcement.Id }, cancellationToken);
        return Ok(ToResponse(announcement, true));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var unread = await Personal(userId).AsTracking().Where(n => !n.IsRead).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);

        var announcementsRead = 0;
        if (TryGetActiveSince(out var activeSince))
        {
            var unreadAnnouncements = await Announcements(activeSince)
                .Where(n => !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.UserId == userId))
                .Select(n => n.Id).ToListAsync(cancellationToken);
            announcementsRead = await MarkAnnouncementsReadAsync(userId, unreadAnnouncements, cancellationToken);
        }
        return Ok(new { updated = unread.Count + announcementsRead });
    }

    private IQueryable<Notification> Personal(int userId) =>
        db.Notifications.AsNoTracking().Where(n => n.Audience == NotificationAudiences.User && n.UserId == userId);

    // Announcements made at or after the moment the customer's account became active.
    private IQueryable<Notification> Announcements(DateTime activeSince) =>
        db.Notifications.AsNoTracking().Where(n => n.Audience == NotificationAudiences.Customers && n.CreatedAtUtc >= activeSince);

    private async Task<int> MarkAnnouncementsReadAsync(int userId, IReadOnlyCollection<int> notificationIds, CancellationToken cancellationToken)
    {
        if (notificationIds.Count == 0) return 0;
        var alreadyRead = await db.NotificationReads.AsNoTracking()
            .Where(r => r.UserId == userId && notificationIds.Contains(r.NotificationId))
            .Select(r => r.NotificationId).ToListAsync(cancellationToken);
        var toAdd = notificationIds.Except(alreadyRead).ToList();
        if (toAdd.Count == 0) return 0;
        foreach (var id in toAdd)
            db.NotificationReads.Add(new NotificationRead { NotificationId = id, UserId = userId });
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // Success only if every read record now exists, i.e. a parallel request (another tab) stored
            // the same reads first. Any other failure is reported, so the client does not show unsaved reads.
            db.ChangeTracker.Clear();
            var stored = await db.NotificationReads.AsNoTracking()
                .Where(r => r.UserId == userId && toAdd.Contains(r.NotificationId))
                .Select(r => r.NotificationId).ToListAsync(cancellationToken);
            if (toAdd.Except(stored).Any()) throw;
        }
        return toAdd.Count;
    }

    // Identity always comes from the validated token, never a request parameter.
    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out userId) && userId > 0;

    // Set by the User Service only for approved customers (approval time, or registration time for
    // older accounts). Without it the customer sees no announcements.
    private bool TryGetActiveSince(out DateTime activeSince)
    {
        activeSince = default;
        if (!long.TryParse(User.FindFirstValue("active_since"), out var seconds)) return false;
        activeSince = DateTimeOffset.FromUnixTimeSeconds(seconds).UtcDateTime;
        return true;
    }

    private static NotificationResponse ToResponse(Notification n, bool isRead) => new()
    {
        Id = n.Id,
        Type = n.Type,
        ReservationId = n.ReservationId,
        BookId = n.BookId,
        BookTitle = n.BookTitle,
        Message = n.Message,
        // MySQL datetime values have no Kind; notifications are written in UTC.
        DueDate = n.DueDate is { } due ? DateTime.SpecifyKind(due, DateTimeKind.Utc) : null,
        ReturnDate = n.ReturnDate is { } returned ? DateTime.SpecifyKind(returned, DateTimeKind.Utc) : null,
        IsRead = isRead,
        CreatedAtUtc = DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc)
    };
}

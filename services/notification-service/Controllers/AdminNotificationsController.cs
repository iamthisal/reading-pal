using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Controllers;

/// <summary>
/// Notifications for library admins. Admins share the notification rows but each has their own read
/// state (NotificationReads, keyed by the admin's token subject). Rows marked read with the older shared
/// IsRead flag stay read for every admin.
/// </summary>
[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
public sealed class AdminNotificationsController(NotificationDbContext db, ICustomerDirectory customers, NotificationReadStore reads) : ControllerBase
{
    private const int MaxPageSize = 50;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminNotificationResponse>>> Get(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var recipientKey = RecipientKey();
        var query = AdminNotifications();
        if (unreadOnly) query = query.Where(n => !n.IsRead && !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.RecipientKey == recipientKey));
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(n => new { Notification = n, IsRead = n.IsRead || db.NotificationReads.Any(r => r.NotificationId == n.Id && r.RecipientKey == recipientKey) })
            .ToListAsync(cancellationToken);
        if (items.Count == 0) return Ok(Array.Empty<AdminNotificationResponse>());

        var names = await customers.GetNamesAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return Ok(items.Select(i => ToResponse(i.Notification, i.IsRead, names)).ToList());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var recipientKey = RecipientKey();
        var count = await AdminNotifications()
            .CountAsync(n => !n.IsRead && !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.RecipientKey == recipientKey), cancellationToken);
        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var exists = await AdminNotifications().AnyAsync(n => n.Id == id, cancellationToken);
        if (!exists) return NotFound(new { message = "Notification not found." });
        // Only this admin's read state changes; other admins keep theirs.
        await reads.MarkReadAsync(RecipientKey(), new[] { id }, cancellationToken);
        return Ok(new { Id = id, IsRead = true });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var recipientKey = RecipientKey();
        var unread = await AdminNotifications()
            .Where(n => !n.IsRead && !db.NotificationReads.Any(r => r.NotificationId == n.Id && r.RecipientKey == recipientKey))
            .Select(n => n.Id).ToListAsync(cancellationToken);
        var updated = await reads.MarkReadAsync(recipientKey, unread, cancellationToken);
        return Ok(new { updated });
    }

    // Admin notifications for this admin: everything except changes they made themselves.
    private IQueryable<Notification> AdminNotifications()
    {
        var subject = AdminSubject();
        return db.Notifications.AsNoTracking()
            .Where(n => n.Audience == NotificationAudiences.Admin && (n.PerformedBy == null || n.PerformedBy != subject));
    }

    // The hardcoded admin's subject is "admin-id"; admins stored in the User Service use their numeric ID.
    private string AdminSubject() => User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub") ?? "admin";

    private string RecipientKey() => RecipientKeys.Admin(AdminSubject());

    private static AdminNotificationResponse ToResponse(Notification n, bool isRead, IReadOnlyDictionary<int, string> names)
    {
        var customerName = names.GetValueOrDefault(n.UserId) ?? NotificationFactory.FallbackCustomerName(n.UserId);
        return new AdminNotificationResponse
        {
            Id = n.Id,
            Type = n.Type,
            ReservationId = n.ReservationId,
            BookId = n.BookId,
            BookTitle = n.BookTitle,
            CustomerUserId = n.UserId,
            CustomerName = customerName,
            // MySQL datetime values have no Kind; notifications are written in UTC.
            ReservationDate = n.ReservationDate is { } reserved ? DateTime.SpecifyKind(reserved, DateTimeKind.Utc) : null,
            Message = n.Message.Replace(NotificationFactory.CustomerPlaceholder, customerName),
            Link = AdminNotificationResponse.LinkFor(n.Type),
            IsRead = isRead,
            CreatedAtUtc = DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc)
        };
    }
}

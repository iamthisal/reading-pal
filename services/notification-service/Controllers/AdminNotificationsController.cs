using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Controllers;

/// <summary>
/// Notifications for library admins about customer actions on pending reservations. Admins share one
/// set of notifications and read state (there is a single admin account).
/// </summary>
[ApiController]
[Route("api/admin/notifications")]
[Authorize(Roles = "Admin")]
public sealed class AdminNotificationsController(NotificationDbContext db, ICustomerDirectory customers) : ControllerBase
{
    private const int MaxPageSize = 50;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<AdminNotificationResponse>>> Get(
        [FromQuery] bool unreadOnly = false, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

        var query = db.Notifications.AsNoTracking().Where(n => n.Audience == NotificationAudiences.Admin);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        if (items.Count == 0) return Ok(Array.Empty<AdminNotificationResponse>());

        var names = await customers.GetNamesAsync(Request.Headers.Authorization.ToString(), cancellationToken);
        return Ok(items.Select(n => ToResponse(n, names)).ToList());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var count = await db.Notifications.CountAsync(n => n.Audience == NotificationAudiences.Admin && !n.IsRead, cancellationToken);
        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        var notification = await db.Notifications.SingleOrDefaultAsync(
            n => n.Id == id && n.Audience == NotificationAudiences.Admin, cancellationToken);
        if (notification == null) return NotFound(new { message = "Notification not found." });
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok(new { notification.Id, notification.IsRead });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        var unread = await db.Notifications.Where(n => n.Audience == NotificationAudiences.Admin && !n.IsRead).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { updated = unread.Count });
    }

    private static AdminNotificationResponse ToResponse(Notification n, IReadOnlyDictionary<int, string> names)
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
            IsRead = n.IsRead,
            CreatedAtUtc = DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc)
        };
    }
}

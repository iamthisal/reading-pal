using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;

namespace NotificationService.Controllers;

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

        var query = db.Notifications.AsNoTracking().Where(n => n.Audience == NotificationAudiences.User && n.UserId == userId);
        if (unreadOnly) query = query.Where(n => !n.IsRead);
        var items = await query
            .OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .ToListAsync(cancellationToken);
        return Ok(items.Select(ToResponse).ToList());
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<UnreadCountResponse>> GetUnreadCount(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var count = await db.Notifications.CountAsync(n => n.Audience == NotificationAudiences.User && n.UserId == userId && !n.IsRead, cancellationToken);
        return Ok(new UnreadCountResponse { Count = count });
    }

    [HttpPost("{id:int}/read")]
    public async Task<IActionResult> MarkRead(int id, CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        // Another user's notification is reported as missing so IDs cannot be probed.
        var notification = await db.Notifications.SingleOrDefaultAsync(n => n.Id == id && n.Audience == NotificationAudiences.User && n.UserId == userId, cancellationToken);
        if (notification == null) return NotFound(new { message = "Notification not found." });
        if (!notification.IsRead)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        return Ok(ToResponse(notification));
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken)
    {
        if (!TryGetUserId(out var userId))
            return Unauthorized(new { message = "Invalid user identity. Please log in again." });
        var unread = await db.Notifications.Where(n => n.Audience == NotificationAudiences.User && n.UserId == userId && !n.IsRead).ToListAsync(cancellationToken);
        var now = DateTime.UtcNow;
        foreach (var notification in unread)
        {
            notification.IsRead = true;
            notification.ReadAtUtc = now;
        }
        await db.SaveChangesAsync(cancellationToken);
        return Ok(new { updated = unread.Count });
    }

    // Identity always comes from the validated token, never a request parameter.
    private bool TryGetUserId(out int userId) =>
        int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub"), out userId) && userId > 0;

    private static NotificationResponse ToResponse(Notification n) => new()
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
        IsRead = n.IsRead,
        CreatedAtUtc = DateTime.SpecifyKind(n.CreatedAtUtc, DateTimeKind.Utc)
    };
}

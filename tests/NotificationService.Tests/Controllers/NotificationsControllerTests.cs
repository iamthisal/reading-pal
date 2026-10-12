using Microsoft.AspNetCore.Mvc;
using NotificationService.Controllers;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Controllers;

public class NotificationsControllerTests
{
    private static NotificationsController CreateController(NotificationDbContext db, string userId = "7")
    {
        var controller = TestFactory.CustomerController(db);
        TestFactory.AttachUser(controller, userId);
        return controller;
    }

    private static async Task SeedAsync(NotificationDbContext db)
    {
        var start = new DateTime(2026, 10, 1, 8, 0, 0, DateTimeKind.Utc);
        db.Notifications.AddRange(
            new Notification { Id = 1, EventId = Guid.NewGuid(), UserId = 7, Type = NotificationTypes.ReservationAccepted, Message = "older", CreatedAtUtc = start },
            new Notification { Id = 2, EventId = Guid.NewGuid(), UserId = 7, Type = NotificationTypes.BookReturned, Message = "newer", CreatedAtUtc = start.AddHours(1) },
            new Notification { Id = 3, EventId = Guid.NewGuid(), UserId = 7, Type = NotificationTypes.ReservationCancelled, Message = "read", CreatedAtUtc = start.AddMinutes(30), IsRead = true },
            new Notification { Id = 4, EventId = Guid.NewGuid(), UserId = 99, Type = NotificationTypes.ReservationAccepted, Message = "someone else", CreatedAtUtc = start.AddHours(2) });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Get_ReturnsOnlyCurrentUsersNotificationsNewestFirst()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        var result = await CreateController(db).Get(cancellationToken: CancellationToken.None);

        var items = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();
        Assert.Equal(new[] { 2, 3, 1 }, items.Select(n => n.Id));
    }

    [Fact]
    public async Task Get_UnreadOnly_ExcludesReadNotifications()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        var result = await CreateController(db).Get(unreadOnly: true, cancellationToken: CancellationToken.None);

        var items = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value);
        Assert.Equal(new[] { 2, 1 }, items.Select(n => n.Id));
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyCurrentUsersUnread()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        var result = await CreateController(db).GetUnreadCount(CancellationToken.None);

        Assert.Equal(2, Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Count);
    }

    [Fact]
    public async Task MarkRead_OwnNotification_MarksItRead()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<OkObjectResult>(await CreateController(db).MarkRead(1, CancellationToken.None));

        var notification = (await db.Notifications.FindAsync(1))!;
        Assert.True(notification.IsRead);
        Assert.NotNull(notification.ReadAtUtc);
    }

    [Fact]
    public async Task MarkRead_AnotherUsersNotification_ReturnsNotFoundAndChangesNothing()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<NotFoundObjectResult>(await CreateController(db).MarkRead(4, CancellationToken.None));
        Assert.False((await db.Notifications.FindAsync(4))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_MarksOnlyCurrentUsersNotifications()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<OkObjectResult>(await CreateController(db).MarkAllRead(CancellationToken.None));

        Assert.All(db.Notifications.Where(n => n.UserId == 7), n => Assert.True(n.IsRead));
        Assert.False((await db.Notifications.FindAsync(4))!.IsRead);
    }

    [Fact]
    public async Task Requests_WithNonNumericIdentity_AreUnauthorized()
    {
        using var db = TestFactory.CreateDbContext();
        // The hardcoded admin account's token carries "admin-id", not a user ID.
        var controller = CreateController(db, userId: "admin-id");

        Assert.IsType<UnauthorizedObjectResult>((await controller.Get(cancellationToken: CancellationToken.None)).Result);
        Assert.IsType<UnauthorizedObjectResult>((await controller.GetUnreadCount(CancellationToken.None)).Result);
    }

    [Fact]
    public async Task UserEndpoints_NeverExposeAdminNotificationsAboutTheUser()
    {
        using var db = TestFactory.CreateDbContext();
        db.Notifications.Add(new Notification { Id = 10, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Admin, UserId = 7,
            Type = NotificationTypes.NewReservation, Message = "{customer} reserved 'Dune'.", CreatedAtUtc = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var controller = CreateController(db);

        var items = Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(Assert.IsType<OkObjectResult>(
            (await controller.Get(cancellationToken: CancellationToken.None)).Result).Value);
        Assert.Empty(items);
        Assert.Equal(0, Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>(
            (await controller.GetUnreadCount(CancellationToken.None)).Result).Value).Count);
        Assert.IsType<NotFoundObjectResult>(await controller.MarkRead(10, CancellationToken.None));
    }
}

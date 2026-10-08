using Microsoft.AspNetCore.Mvc;
using NotificationService.Controllers;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Controllers;

public class AnnouncementVisibilityTests
{
    private static readonly DateTime AnnouncedAt = new(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc);

    private static NotificationsController Controller(NotificationDbContext db, int userId, DateTime? activeSince)
    {
        var controller = new NotificationsController(db);
        TestFactory.AttachUser(controller, userId.ToString(), activeSince: activeSince);
        return controller;
    }

    private static async Task SeedAsync(NotificationDbContext db)
    {
        db.Notifications.AddRange(
            new Notification { Id = 1, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Customers, UserId = 0,
                Type = NotificationTypes.NewBook, BookId = 12, BookTitle = "Clean Code", Message = "New in the catalogue: 'Clean Code'.", CreatedAtUtc = AnnouncedAt },
            new Notification { Id = 2, EventId = Guid.NewGuid(), Audience = NotificationAudiences.User, UserId = 7,
                Type = NotificationTypes.BookReturned, Message = "returned", CreatedAtUtc = AnnouncedAt.AddHours(-1) });
        await db.SaveChangesAsync();
    }

    private static List<NotificationResponse> Items(ActionResult<IEnumerable<NotificationResponse>> result) =>
        Assert.IsAssignableFrom<IEnumerable<NotificationResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

    private static int Count(ActionResult<UnreadCountResponse> result) =>
        Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Count;

    [Fact]
    public async Task CustomerActiveBeforeAnnouncement_SeesItUnreadWithTitle()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var controller = Controller(db, 7, activeSince: AnnouncedAt.AddDays(-30));

        var items = Items(await controller.Get(cancellationToken: CancellationToken.None));

        Assert.Equal(new[] { 1, 2 }, items.Select(n => n.Id));
        var announcement = items[0];
        Assert.Equal(NotificationTypes.NewBook, announcement.Type);
        Assert.Equal("Clean Code", announcement.BookTitle);
        Assert.False(announcement.IsRead);
        Assert.Equal(2, Count(await controller.GetUnreadCount(CancellationToken.None)));
    }

    [Fact]
    public async Task CustomerActivatedAfterAnnouncement_DoesNotSeeIt()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var controller = Controller(db, 8, activeSince: AnnouncedAt.AddMinutes(1));

        Assert.Empty(Items(await controller.Get(cancellationToken: CancellationToken.None)));
        Assert.Equal(0, Count(await controller.GetUnreadCount(CancellationToken.None)));
        Assert.IsType<NotFoundObjectResult>(await controller.MarkRead(1, CancellationToken.None));
    }

    [Fact]
    public async Task UnapprovedCustomerWithoutActiveSince_SeesNoAnnouncements()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.Equal(new[] { 2 }, Items(await Controller(db, 7, activeSince: null).Get(cancellationToken: CancellationToken.None)).Select(n => n.Id));
    }

    [Fact]
    public async Task ReadingAnAnnouncement_IsPerCustomer()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var first = Controller(db, 7, activeSince: AnnouncedAt.AddDays(-1));
        var second = Controller(db, 9, activeSince: AnnouncedAt.AddDays(-1));

        Assert.IsType<OkObjectResult>(await first.MarkRead(1, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await first.MarkRead(1, CancellationToken.None));

        Assert.True(Items(await first.Get(cancellationToken: CancellationToken.None)).Single(n => n.Id == 1).IsRead);
        Assert.False(Items(await second.Get(cancellationToken: CancellationToken.None)).Single(n => n.Id == 1).IsRead);
        Assert.Single(db.NotificationReads);
        // The shared announcement row itself is never marked read.
        Assert.False((await db.Notifications.FindAsync(1))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_CoversAnnouncementsAndUnreadOnlyHidesThem()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var controller = Controller(db, 7, activeSince: AnnouncedAt.AddDays(-1));

        Assert.IsType<OkObjectResult>(await controller.MarkAllRead(CancellationToken.None));

        Assert.Equal(0, Count(await controller.GetUnreadCount(CancellationToken.None)));
        Assert.Empty(Items(await controller.Get(unreadOnly: true, cancellationToken: CancellationToken.None)));
    }
}

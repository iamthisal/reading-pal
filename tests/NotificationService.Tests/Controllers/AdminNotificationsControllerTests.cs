using Microsoft.AspNetCore.Mvc;
using NotificationService.Controllers;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Controllers;

public class AdminNotificationsControllerTests
{
    private static AdminNotificationsController CreateController(NotificationDbContext db, FakeCustomerDirectory? directory = null)
    {
        var controller = new AdminNotificationsController(db, directory ?? new FakeCustomerDirectory(new Dictionary<int, string> { [7] = "Kamal Perera" }));
        TestFactory.AttachUser(controller, "admin-id", role: "Admin");
        controller.HttpContext.Request.Headers.Authorization = "Bearer admin-token";
        return controller;
    }

    private static async Task SeedAsync(NotificationDbContext db)
    {
        var start = new DateTime(2026, 10, 3, 8, 0, 0, DateTimeKind.Utc);
        db.Notifications.AddRange(
            new Notification { Id = 1, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Admin, UserId = 7, Type = NotificationTypes.NewReservation,
                BookTitle = "Clean Code", Message = "{customer} reserved 'Clean Code' on 3 Oct 2026 at 1:30 PM.", ReservationDate = start, CreatedAtUtc = start },
            new Notification { Id = 2, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Admin, UserId = 8, Type = NotificationTypes.CustomerCancelledReservation,
                BookTitle = "Dune", Message = "{customer} cancelled their pending reservation for 'Dune'.", CreatedAtUtc = start.AddMinutes(5) },
            new Notification { Id = 3, EventId = Guid.NewGuid(), Audience = NotificationAudiences.User, UserId = 7, Type = NotificationTypes.ReservationCancelled,
                Message = "You cancelled your reservation for 'Dune'.", CreatedAtUtc = start.AddMinutes(10) });
        await db.SaveChangesAsync();
    }

    private static List<AdminNotificationResponse> Items(ActionResult<IEnumerable<AdminNotificationResponse>> result) =>
        Assert.IsAssignableFrom<IEnumerable<AdminNotificationResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

    [Fact]
    public async Task Get_ReturnsOnlyAdminNotificationsWithCustomerNamesAndPendingLink()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var directory = new FakeCustomerDirectory(new Dictionary<int, string> { [7] = "Kamal Perera" });

        var items = Items(await CreateController(db, directory).Get(cancellationToken: CancellationToken.None));

        Assert.Equal(new[] { 2, 1 }, items.Select(n => n.Id));
        var created = items.Single(n => n.Id == 1);
        Assert.Equal("Kamal Perera reserved 'Clean Code' on 3 Oct 2026 at 1:30 PM.", created.Message);
        Assert.Equal("Kamal Perera", created.CustomerName);
        Assert.Equal(7, created.CustomerUserId);
        Assert.Equal("/admin/reservations/pending", created.Link);
        // Names are looked up with the calling admin's own token.
        Assert.Equal("Bearer admin-token", directory.LastAuthorizationHeader);
    }

    [Fact]
    public async Task Get_WhenCustomerNameUnavailable_FallsBackToCustomerId()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        var items = Items(await CreateController(db, new FakeCustomerDirectory(new Dictionary<int, string>())).Get(cancellationToken: CancellationToken.None));

        Assert.Equal("Customer #8 cancelled their pending reservation for 'Dune'.", items.Single(n => n.Id == 2).Message);
    }

    [Fact]
    public async Task GetUnreadCount_CountsOnlyAdminNotifications()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        var result = await CreateController(db).GetUnreadCount(CancellationToken.None);

        Assert.Equal(2, Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Count);
    }

    [Fact]
    public async Task MarkRead_CustomerNotification_ReturnsNotFound()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<NotFoundObjectResult>(await CreateController(db).MarkRead(3, CancellationToken.None));
        Assert.False((await db.Notifications.FindAsync(3))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_LeavesCustomerNotificationsUntouched()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<OkObjectResult>(await CreateController(db).MarkAllRead(CancellationToken.None));

        Assert.All(db.Notifications.Where(n => n.Audience == NotificationAudiences.Admin), n => Assert.True(n.IsRead));
        Assert.False((await db.Notifications.FindAsync(3))!.IsRead);
    }
}

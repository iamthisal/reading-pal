using Microsoft.AspNetCore.Mvc;
using NotificationService.Controllers;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Controllers;

public class AdminNotificationsControllerTests
{
    private static AdminNotificationsController CreateController(NotificationDbContext db, FakeCustomerDirectory? directory = null, string adminId = "admin-id")
    {
        var controller = TestFactory.AdminController(db, directory ?? new FakeCustomerDirectory(new Dictionary<int, string> { [7] = "Kamal Perera" }));
        TestFactory.AttachUser(controller, adminId, role: "Admin");
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

        // Read records for this admin only; the customer notification is untouched.
        Assert.Equal(new[] { 1, 2 }, db.NotificationReads.Where(r => r.RecipientKey == "admin:admin-id").Select(r => r.NotificationId).OrderBy(id => id));
        Assert.False((await db.Notifications.FindAsync(3))!.IsRead);
        Assert.Equal(0, Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>((await CreateController(db).GetUnreadCount(CancellationToken.None)).Result).Value).Count);
    }

    [Fact]
    public async Task MarkRead_ChangesOnlyThisAdminsReadState()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);

        Assert.IsType<OkObjectResult>(await CreateController(db, adminId: "admin-id").MarkRead(1, CancellationToken.None));

        Assert.True(Items(await CreateController(db, adminId: "admin-id").Get(cancellationToken: CancellationToken.None)).Single(n => n.Id == 1).IsRead);
        Assert.False(Items(await CreateController(db, adminId: "12").Get(cancellationToken: CancellationToken.None)).Single(n => n.Id == 1).IsRead);
        // The shared row is not flagged; read state lives per admin.
        Assert.False((await db.Notifications.FindAsync(1))!.IsRead);
    }

    [Fact]
    public async Task MarkAllRead_RepeatedSucceedsSafely()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        var controller = CreateController(db);

        Assert.IsType<OkObjectResult>(await controller.MarkAllRead(CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.MarkAllRead(CancellationToken.None));

        Assert.Equal(2, db.NotificationReads.Count());
    }

    [Fact]
    public async Task NotificationReadUnderOldSharedFlag_StaysReadForEveryAdmin()
    {
        using var db = TestFactory.CreateDbContext();
        await SeedAsync(db);
        (await db.Notifications.FindAsync(2))!.IsRead = true;
        await db.SaveChangesAsync();

        Assert.True(Items(await CreateController(db, adminId: "12").Get(cancellationToken: CancellationToken.None)).Single(n => n.Id == 2).IsRead);
        Assert.Equal(1, Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>((await CreateController(db, adminId: "12").GetUnreadCount(CancellationToken.None)).Result).Value).Count);
    }
}

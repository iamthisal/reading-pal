using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Controllers;
using NotificationService.Models;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Controllers;

// Review finding on #83: mark-as-read must not report success unless the read is actually stored.
public class AnnouncementReadPersistenceTests
{
    private static readonly DateTime AnnouncedAt = new(2026, 10, 8, 5, 0, 0, DateTimeKind.Utc);

    private static async Task SeedAnnouncementAsync(string databaseName)
    {
        using var db = TestFactory.CreateDbContext(databaseName);
        db.Notifications.Add(new Notification { Id = 1, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Customers, UserId = 0,
            Type = NotificationTypes.NewBook, BookTitle = "Clean Code", Message = "New in the catalogue: 'Clean Code'.", CreatedAtUtc = AnnouncedAt });
        await db.SaveChangesAsync();
    }

    private static NotificationsController Controller(string databaseName, BeforeFirstSaveInterceptor interceptor)
    {
        var controller = TestFactory.CustomerController(TestFactory.CreateDbContext(databaseName, interceptor));
        TestFactory.AttachUser(controller, "7", activeSince: AnnouncedAt.AddDays(-1));
        return controller;
    }

    [Fact]
    public async Task MarkRead_WhenSaveFailsWithoutStoringTheRead_ReportsTheFailure()
    {
        var name = Guid.NewGuid().ToString();
        await SeedAnnouncementAsync(name);
        var controller = Controller(name, new BeforeFirstSaveInterceptor(() => throw new DbUpdateException("Simulated database failure.")));

        await Assert.ThrowsAsync<DbUpdateException>(() => controller.MarkRead(1, CancellationToken.None));

        using var check = TestFactory.CreateDbContext(name);
        Assert.Empty(check.NotificationReads);
    }

    [Fact]
    public async Task MarkAllRead_WhenSaveFailsWithoutStoringTheReads_ReportsTheFailure()
    {
        var name = Guid.NewGuid().ToString();
        await SeedAnnouncementAsync(name);
        var controller = Controller(name, new BeforeFirstSaveInterceptor(() => throw new DbUpdateException("Simulated database failure.")));

        // The first save (personal notifications) fails, so nothing is reported as read.
        await Assert.ThrowsAsync<DbUpdateException>(() => controller.MarkAllRead(CancellationToken.None));
    }

    [Fact]
    public async Task MarkRead_WhenAnotherTabStoredTheSameReadFirst_Succeeds()
    {
        // SQLite enforces the unique (NotificationId, UserId) index like MySQL does.
        using var database = new SqliteTestDatabase();
        using (var seed = database.CreateContext())
        {
            seed.Notifications.Add(new Notification { Id = 1, EventId = Guid.NewGuid(), Audience = NotificationAudiences.Customers, UserId = 0,
                Type = NotificationTypes.NewBook, BookTitle = "Clean Code", Message = "New in the catalogue: 'Clean Code'.", CreatedAtUtc = AnnouncedAt });
            await seed.SaveChangesAsync();
        }
        var controller = TestFactory.CustomerController(database.CreateContext(new BeforeFirstSaveInterceptor(async () =>
        {
            using var other = database.CreateContext();
            other.NotificationReads.Add(new NotificationRead { NotificationId = 1, RecipientKey = "user:7" });
            await other.SaveChangesAsync();
        })));
        TestFactory.AttachUser(controller, "7", activeSince: AnnouncedAt.AddDays(-1));

        Assert.IsType<OkObjectResult>(await controller.MarkRead(1, CancellationToken.None));

        using var check = database.CreateContext();
        Assert.Single(check.NotificationReads);
    }
}

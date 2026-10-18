using Microsoft.AspNetCore.Mvc;
using NotificationService.Controllers;
using NotificationService.Data;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class AdminCatalogNotificationTests
{
    // The shape Inventory publishes, including who made the change and what they did.
    private static string BookEvent(string type, string? action, string? performedBy = "12", string? email = "admin2@library.test",
        Guid? eventId = null, string title = "Clean Code") => TestFactory.EventJson(new
    {
        EventId = eventId ?? Guid.NewGuid(),
        EventType = type,
        TimestampUtc = DateTime.UtcNow,
        BookId = 12,
        Book = new { Id = 12, Title = title, Author = "Robert C. Martin" },
        Action = action,
        PerformedBy = performedBy,
        PerformedByEmail = email
    });

    private static AdminNotificationsController Admin(NotificationDbContext db, string subject)
    {
        var controller = TestFactory.AdminController(db, new FakeCustomerDirectory(new Dictionary<int, string>()));
        TestFactory.AttachUser(controller, subject, role: "Admin");
        return controller;
    }

    private static List<AdminNotificationResponse> Items(ActionResult<IEnumerable<AdminNotificationResponse>> result) =>
        Assert.IsAssignableFrom<IEnumerable<AdminNotificationResponse>>(Assert.IsType<OkObjectResult>(result.Result).Value).ToList();

    private static int UnreadCount(ActionResult<UnreadCountResponse> result) =>
        Assert.IsType<UnreadCountResponse>(Assert.IsType<OkObjectResult>(result.Result).Value).Count;

    [Theory]
    [InlineData("book-created", "created", NotificationTypes.AdminBookCreated, "admin2@library.test added 'Clean Code' to the catalogue.")]
    [InlineData("book-updated", "updated", NotificationTypes.AdminBookUpdated, "admin2@library.test updated 'Clean Code'.")]
    [InlineData("book-updated", "marked-unavailable", NotificationTypes.AdminBookUpdated, "admin2@library.test marked 'Clean Code' as unavailable.")]
    [InlineData("book-updated", "marked-available", NotificationTypes.AdminBookUpdated, "admin2@library.test marked 'Clean Code' as available.")]
    [InlineData("book-deleted", "deleted", NotificationTypes.AdminBookDeleted, "admin2@library.test deleted 'Clean Code' from the catalogue.")]
    public async Task CatalogChange_CreatesAdminNoticeIdentifyingBookActionAndActor(string type, string action, string expectedType, string expectedMessage)
    {
        using var db = TestFactory.CreateDbContext();

        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent(type, action), CancellationToken.None);

        var notice = Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Admin);
        Assert.Equal(expectedType, notice.Type);
        Assert.Equal(expectedMessage, notice.Message);
        Assert.Equal("Clean Code", notice.BookTitle);
        Assert.Equal("12", notice.PerformedBy);
    }

    [Fact]
    public async Task CatalogChange_ReceivedTwice_CreatesOneAdminNotice()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateCatalogHandler(db);
        var json = BookEvent("book-updated", "updated");

        Assert.Equal(HandleOutcome.Created, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Single(db.Notifications);
    }

    [Fact]
    public async Task AdminWhoMadeTheChange_DoesNotSeeIt_OtherAdminsDoUnread()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-created", "created", performedBy: "12"), CancellationToken.None);
        var noticeId = Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Admin).Id;

        var actor = Admin(db, "12");
        Assert.Empty(Items(await actor.Get(cancellationToken: CancellationToken.None)));
        Assert.Equal(0, UnreadCount(await actor.GetUnreadCount(CancellationToken.None)));
        Assert.IsType<NotFoundObjectResult>(await actor.MarkRead(noticeId, CancellationToken.None));

        var other = Admin(db, "admin-id");
        var seen = Assert.Single(Items(await other.Get(cancellationToken: CancellationToken.None)));
        Assert.False(seen.IsRead);
        Assert.Equal(NotificationTypes.AdminBookCreated, seen.Type);
        Assert.Equal(1, UnreadCount(await other.GetUnreadCount(CancellationToken.None)));
    }

    [Fact]
    public async Task DeletedBookNotice_KeepsTitleAndHasNoLink()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-deleted", "deleted", title: "Gone Book"), CancellationToken.None);

        var seen = Assert.Single(Items(await Admin(db, "admin-id").Get(cancellationToken: CancellationToken.None)));

        Assert.Equal("Gone Book", seen.BookTitle);
        Assert.Contains("'Gone Book'", seen.Message);
        Assert.Null(seen.Link);
    }

    [Fact]
    public async Task AddedOrUpdatedBookNotice_LinksToBookInventory()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-updated", "updated"), CancellationToken.None);

        Assert.Equal("/admin/books", Assert.Single(Items(await Admin(db, "admin-id").Get(cancellationToken: CancellationToken.None))).Link);
    }

    [Fact]
    public async Task OlderEventWithoutActor_IsShownToEveryAdmin()
    {
        using var db = TestFactory.CreateDbContext();
        await TestFactory.CreateCatalogHandler(db).HandleAsync(BookEvent("book-updated", null, performedBy: null, email: null), CancellationToken.None);

        var seen = Assert.Single(Items(await Admin(db, "12").Get(cancellationToken: CancellationToken.None)));
        Assert.Equal("Another admin updated 'Clean Code'.", seen.Message);
        Assert.Single(Items(await Admin(db, "admin-id").Get(cancellationToken: CancellationToken.None)));
    }
}

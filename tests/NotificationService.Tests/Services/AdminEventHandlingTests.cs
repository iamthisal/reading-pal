using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class AdminEventHandlingTests
{
    private static string Json(string type, Guid? eventId = null, string? cancelledBy = null) => TestFactory.EventJson(new
    {
        EventId = eventId ?? Guid.NewGuid(),
        EventType = type,
        SchemaVersion = 2,
        TimestampUtc = DateTime.UtcNow,
        ReservationId = 5,
        UserId = 7,
        BookId = 12,
        ReservationDate = DateTime.UtcNow,
        CancelledBy = cancelledBy,
        DueDate = type == "reservation-accepted" ? DateTime.UtcNow.AddDays(14) : (DateTime?)null
    });

    [Fact]
    public async Task ReservationCreated_CreatesOnlyAnAdminNotification()
    {
        using var db = TestFactory.CreateDbContext();

        var outcome = await TestFactory.CreateHandler(db).HandleAsync(Json("reservation-created"), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        var notification = Assert.Single(db.Notifications);
        Assert.Equal(NotificationAudiences.Admin, notification.Audience);
        Assert.Equal(NotificationTypes.NewReservation, notification.Type);
        Assert.Equal("Clean Code", notification.BookTitle);
    }

    [Fact]
    public async Task CustomerCancellation_NotifiesBothCustomerAndAdmins_FromOneEvent()
    {
        using var db = TestFactory.CreateDbContext();

        await TestFactory.CreateHandler(db).HandleAsync(Json("reservation-cancelled", cancelledBy: "User"), CancellationToken.None);

        var audiences = db.Notifications.Select(n => n.Audience).OrderBy(a => a).ToList();
        Assert.Equal(new[] { NotificationAudiences.Admin, NotificationAudiences.User }, audiences);
        Assert.Single(db.Notifications.Select(n => n.EventId).Distinct());
    }

    [Fact]
    public async Task CustomerCancellation_ReceivedTwice_CreatesNoExtraNotifications()
    {
        using var db = TestFactory.CreateDbContext();
        var lookup = new FixedTitleLookup("Clean Code");
        var handler = new LendingEventHandler(db, lookup, NullLogger<LendingEventHandler>.Instance);
        var json = Json("reservation-cancelled", cancelledBy: "User");

        await handler.HandleAsync(json, CancellationToken.None);
        var second = await handler.HandleAsync(json, CancellationToken.None);

        Assert.Equal(HandleOutcome.Duplicate, second);
        Assert.Equal(2, db.Notifications.Count());
        Assert.Equal(1, lookup.Calls);
    }

    [Fact]
    public async Task PartiallyStoredEvent_AddsOnlyTheMissingAudience()
    {
        // A notification stored by YRP-208 before admin notifications existed: the event is replayed after upgrade.
        using var db = TestFactory.CreateDbContext();
        var eventId = Guid.NewGuid();
        db.Notifications.Add(new Notification { EventId = eventId, Audience = NotificationAudiences.User, UserId = 7, Type = NotificationTypes.ReservationCancelled });
        await db.SaveChangesAsync();

        var outcome = await TestFactory.CreateHandler(db).HandleAsync(Json("reservation-cancelled", eventId, "User"), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        Assert.Equal(2, db.Notifications.Count());
        Assert.Single(db.Notifications, n => n.Audience == NotificationAudiences.Admin);
    }

    [Theory]
    [InlineData("reservation-accepted", null)]
    [InlineData("reservation-cancelled", "Admin")]
    public async Task AdminAction_CreatesNoAdminNotification(string type, string? cancelledBy)
    {
        using var db = TestFactory.CreateDbContext();

        await TestFactory.CreateHandler(db).HandleAsync(Json(type, cancelledBy: cancelledBy), CancellationToken.None);

        // The customer is still told; the admin who performed the action is not.
        var notification = Assert.Single(db.Notifications);
        Assert.Equal(NotificationAudiences.User, notification.Audience);
    }
}

using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class LendingEventHandlerTests
{
    private static string AcceptedJson(Guid eventId, int userId = 7) => TestFactory.EventJson(new
    {
        EventId = eventId,
        EventType = "reservation-accepted",
        SchemaVersion = 2,
        TimestampUtc = DateTime.UtcNow,
        ReservationId = 5,
        UserId = userId,
        BookId = 12,
        ReservationDate = DateTime.UtcNow.AddDays(-1),
        Status = "Borrowed",
        CheckoutDate = DateTime.UtcNow,
        DueDate = DateTime.UtcNow.AddDays(14)
    });

    [Fact]
    public async Task HandleAsync_NewEvent_CreatesOneUnreadNotification()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);

        var outcome = await handler.HandleAsync(AcceptedJson(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal(HandleOutcome.Created, outcome);
        var notification = Assert.Single(db.Notifications);
        Assert.Equal(7, notification.UserId);
        Assert.Equal("Clean Code", notification.BookTitle);
        Assert.NotNull(notification.DueDate);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public async Task HandleAsync_SameEventReceivedTwice_CreatesOnlyOneNotification()
    {
        using var db = TestFactory.CreateDbContext();
        var lookup = new FixedTitleLookup("Clean Code");
        var handler = new LendingEventHandler(db, lookup, Microsoft.Extensions.Logging.Abstractions.NullLogger<LendingEventHandler>.Instance);
        var json = AcceptedJson(Guid.NewGuid());

        Assert.Equal(HandleOutcome.Created, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));

        Assert.Single(db.Notifications);
        // The duplicate is detected before any Inventory call.
        Assert.Equal(1, lookup.Calls);
    }

    [Fact]
    public async Task HandleAsync_DifferentEventsForSameReservation_CreateSeparateNotifications()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);

        await handler.HandleAsync(AcceptedJson(Guid.NewGuid()), CancellationToken.None);
        await handler.HandleAsync(TestFactory.EventJson(new
        {
            EventId = Guid.NewGuid(), EventType = "book-returned", SchemaVersion = 1,
            ReservationId = 5, BorrowRecordId = 3, UserId = 7, BookId = 12, ReturnDate = DateTime.UtcNow
        }), CancellationToken.None);

        Assert.Equal(2, db.Notifications.Count());
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    [InlineData("{\"eventType\":\"reservation-accepted\",\"userId\":7,\"bookId\":12}")]
    public async Task HandleAsync_UnusableMessage_IsSkippedWithoutSaving(string payload)
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);

        Assert.Equal(HandleOutcome.Skipped, await handler.HandleAsync(payload, CancellationToken.None));
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task HandleAsync_UnsupportedEventType_IsSkipped()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db);

        var outcome = await handler.HandleAsync(TestFactory.EventJson(new
        {
            EventId = Guid.NewGuid(), EventType = "book-created", UserId = 7, BookId = 12
        }), CancellationToken.None);

        Assert.Equal(HandleOutcome.Skipped, outcome);
        Assert.Empty(db.Notifications);
    }

    [Fact]
    public async Task HandleAsync_WhenTitleUnavailable_StillCreatesNotificationWithFallbackTitle()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = TestFactory.CreateHandler(db, title: null);

        await handler.HandleAsync(AcceptedJson(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("Book #12", Assert.Single(db.Notifications).BookTitle);
    }
}

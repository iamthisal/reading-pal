using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class FineRecordedNotificationTests
{
    private static LendingEvent Fine(int? days, decimal? amount) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = LendingEventTypes.FineRecorded,
        ReservationId = 5,
        UserId = 7,
        BookId = 12,
        DaysOverdue = days,
        Amount = amount
    };

    private static string FineJson(Guid eventId, int days = 3, decimal amount = 30m) => TestFactory.EventJson(new
    {
        EventId = eventId,
        EventType = "fine-recorded",
        SchemaVersion = 1,
        TimestampUtc = DateTime.UtcNow,
        ReservationId = 5,
        BorrowRecordId = 9,
        BookId = 12,
        UserId = 7,
        DaysOverdue = days,
        DailyRate = 10m,
        Amount = amount,
        FineStatus = "Unpaid",
        ReturnDate = DateTime.UtcNow
    });

    [Fact]
    public void CreateForUser_PositiveFine_ShowsTitleOverdueDaysAndRupees()
    {
        var notification = NotificationFactory.CreateForUser(Fine(3, 30m), "Clean Code")!;

        Assert.Equal(NotificationTypes.FineRecorded, notification.Type);
        Assert.Equal(NotificationAudiences.User, notification.Audience);
        Assert.Equal("A fine of Rs. 30.00 was recorded for 'Clean Code': returned 3 days late.", notification.Message);
    }

    [Fact]
    public void CreateForUser_OneDayLate_UsesSingularDay()
    {
        Assert.Equal("A fine of Rs. 10.00 was recorded for 'Clean Code': returned 1 day late.",
            NotificationFactory.CreateForUser(Fine(1, 10m), "Clean Code")!.Message);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(null, null)]
    [InlineData(3, 0)]
    public void CreateForUser_FineWithoutPositiveAmount_CreatesNothing(int? days, int? amount)
    {
        Assert.Null(NotificationFactory.CreateForUser(Fine(days, amount), "Clean Code"));
    }

    [Fact]
    public void CreateForAdmin_FineRecorded_CreatesNothing()
    {
        Assert.Null(NotificationFactory.CreateForAdmin(Fine(3, 30m), "Clean Code"));
    }

    [Fact]
    public async Task HandleAsync_FineEventReceivedAgain_CreatesNoDuplicate()
    {
        using var db = TestFactory.CreateDbContext();
        var lookup = new FixedTitleLookup("Clean Code");
        var handler = new LendingEventHandler(db, lookup, NullLogger<LendingEventHandler>.Instance);
        var json = FineJson(Guid.NewGuid());

        Assert.Equal(HandleOutcome.Created, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));

        var notification = Assert.Single(db.Notifications);
        Assert.Equal(NotificationTypes.FineRecorded, notification.Type);
        Assert.Equal(7, notification.UserId);
        Assert.Equal(1, lookup.Calls);
    }

    [Fact]
    public async Task HandleAsync_ZeroAmountFineEvent_IsSkipped()
    {
        using var db = TestFactory.CreateDbContext();

        var outcome = await TestFactory.CreateHandler(db).HandleAsync(FineJson(Guid.NewGuid(), days: 0, amount: 0m), CancellationToken.None);

        Assert.Equal(HandleOutcome.Skipped, outcome);
        Assert.Empty(db.Notifications);
    }
}

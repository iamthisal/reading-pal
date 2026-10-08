using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;
using NotificationService.Tests.TestSupport;

namespace NotificationService.Tests.Services;

public class DueDateReminderNotificationTests
{
    // 10 Oct 2026, 12:00 in Sri Lanka.
    private static readonly DateTime Due = new(2026, 10, 10, 6, 30, 0, DateTimeKind.Utc);

    private static LendingEvent DueSoon(int? daysUntilDue, DateTime? dueDate = null) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = LendingEventTypes.BookDueSoon,
        ReservationId = 5,
        UserId = 7,
        BookId = 12,
        DueDate = dueDate ?? Due,
        DaysUntilDue = daysUntilDue
    };

    [Fact]
    public void CreateForUser_TwoDaysBefore_ShowsTitleAndDueDate()
    {
        var notification = NotificationFactory.CreateForUser(DueSoon(2), "Clean Code")!;

        Assert.Equal(NotificationTypes.DueDateReminder, notification.Type);
        Assert.Equal("Reminder: 'Clean Code' is due on 10 Oct 2026. Please return it on time to avoid a fine.", notification.Message);
        Assert.Equal(Due, notification.DueDate);
    }

    [Theory]
    [InlineData(1, "tomorrow (10 Oct 2026)")]
    [InlineData(0, "today (10 Oct 2026)")]
    public void CreateForUser_CatchUpReminder_SaysTodayOrTomorrowWithActualDate(int days, string expected)
    {
        Assert.Contains($"is due {expected}.", NotificationFactory.CreateForUser(DueSoon(days), "Clean Code")!.Message);
    }

    [Fact]
    public void CreateForUser_UsesSriLankanDate()
    {
        // 20:00 UTC on 9 Oct is already 10 Oct in Sri Lanka.
        var notification = NotificationFactory.CreateForUser(DueSoon(2, new DateTime(2026, 10, 9, 20, 0, 0, DateTimeKind.Utc)), "Clean Code")!;

        Assert.Contains("10 Oct 2026", notification.Message);
    }

    [Fact]
    public void CreateForAdmin_DueSoon_CreatesNothing()
    {
        Assert.Null(NotificationFactory.CreateForAdmin(DueSoon(2), "Clean Code"));
    }

    [Fact]
    public async Task HandleAsync_ReminderReceivedAgain_CreatesNoDuplicate()
    {
        using var db = TestFactory.CreateDbContext();
        var handler = new LendingEventHandler(db, new FixedTitleLookup("Clean Code"), NullLogger<LendingEventHandler>.Instance);
        var json = TestFactory.EventJson(new
        {
            EventId = Guid.NewGuid(), EventType = "book-due-soon", SchemaVersion = 1, TimestampUtc = DateTime.UtcNow,
            ReservationId = 5, BorrowRecordId = 9, BookId = 12, UserId = 7, DueDate = Due, DaysUntilDue = 2
        });

        Assert.Equal(HandleOutcome.Created, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(HandleOutcome.Duplicate, await handler.HandleAsync(json, CancellationToken.None));
        Assert.Equal(NotificationTypes.DueDateReminder, Assert.Single(db.Notifications).Type);
    }
}

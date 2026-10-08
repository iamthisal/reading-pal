using System.Text.Json;
using LendingService.Data;
using LendingService.Kafka;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;

namespace LendingService.Tests.Kafka;

public class DueDateReminderServiceTests
{
    // 1 Oct 2026, 10:00 in Sri Lanka (UTC+5:30).
    private static readonly DateTime Now = new(2026, 10, 1, 4, 30, 0, DateTimeKind.Utc);

    // A due date at 12:00 Sri Lanka time, the given number of Sri Lankan calendar days from Now.
    private static DateTime DueInDays(int days) => new DateTime(2026, 10, 1, 6, 30, 0, DateTimeKind.Utc).AddDays(days);

    private static DueDateReminderService CreateService(LendingDbContext context) =>
        new(context, NullLogger<DueDateReminderService>.Instance);

    private static async Task SeedLoanAsync(LendingDbContext context, int id, DateTime dueDate,
        DateTime? returnDate = null, DateTime? returnRequestedAt = null, DateTime? reminderSentAt = null)
    {
        context.Reservations.Add(new Reservation { Id = id, BookId = 100 + id, UserId = 7,
            Status = returnDate != null ? "Returned" : returnRequestedAt != null ? "Returning" : "Borrowed" });
        context.BorrowRecords.Add(new BorrowRecord { Id = id, ReservationId = id, BookId = 100 + id, UserId = 7,
            CheckoutDate = dueDate.AddDays(-14), DueDate = dueDate, ReturnDate = returnDate,
            ReturnRequestedAtUtc = returnRequestedAt, DueReminderSentAtUtc = reminderSentAt });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task DueInTwoCalendarDays_QueuesOneReminderWithBookAndDueDate()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoanAsync(context, 1, DueInDays(2));

        Assert.Equal(1, await CreateService(context).QueueRemindersAsync(Now, CancellationToken.None));

        var message = Assert.Single(context.ReservationEvents);
        Assert.Equal("book-due-soon", message.EventType);
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(101, payload.RootElement.GetProperty("bookId").GetInt32());
        Assert.Equal(7, payload.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(DueInDays(2), payload.RootElement.GetProperty("dueDate").GetDateTime().ToUniversalTime());
        Assert.Equal(2, payload.RootElement.GetProperty("daysUntilDue").GetInt32());
        Assert.Equal(Now, (await context.BorrowRecords.FindAsync(1))!.DueReminderSentAtUtc);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    [InlineData(-1)]
    public async Task OutsideReminderWindow_QueuesNothing(int days)
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoanAsync(context, 1, DueInDays(days));

        Assert.Equal(0, await CreateService(context).QueueRemindersAsync(Now, CancellationToken.None));
        Assert.Empty(context.ReservationEvents);
    }

    [Fact]
    public async Task ReturnedOrAwaitingReturnConfirmation_QueuesNothing()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoanAsync(context, 1, DueInDays(2), returnDate: Now.AddHours(-1));
        await SeedLoanAsync(context, 2, DueInDays(2), returnRequestedAt: Now.AddHours(-1));

        Assert.Equal(0, await CreateService(context).QueueRemindersAsync(Now, CancellationToken.None));
        Assert.Empty(context.ReservationEvents);
    }

    [Fact]
    public async Task ReminderAlreadySent_RunningAgainCreatesNoDuplicate()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoanAsync(context, 1, DueInDays(2));
        await SeedLoanAsync(context, 2, DueInDays(1), reminderSentAt: Now.AddDays(-1));
        var service = CreateService(context);

        Assert.Equal(1, await service.QueueRemindersAsync(Now, CancellationToken.None));
        Assert.Equal(0, await service.QueueRemindersAsync(Now.AddHours(1), CancellationToken.None));
        Assert.Equal(0, await service.QueueRemindersAsync(Now.AddDays(1), CancellationToken.None));

        var message = Assert.Single(context.ReservationEvents);
        Assert.Equal(1, message.ReservationId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public async Task MissedReminder_DueTodayOrTomorrow_QueuesOneWithActualDueDate(int days)
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoanAsync(context, 1, DueInDays(days));

        Assert.Equal(1, await CreateService(context).QueueRemindersAsync(Now, CancellationToken.None));

        using var payload = JsonDocument.Parse(Assert.Single(context.ReservationEvents).Payload);
        Assert.Equal(DueInDays(days), payload.RootElement.GetProperty("dueDate").GetDateTime().ToUniversalTime());
        Assert.Equal(days, payload.RootElement.GetProperty("daysUntilDue").GetInt32());
    }

    [Fact]
    public void DaysUntilDue_UsesSriLankanCalendarDays()
    {
        // 18:30 UTC on 1 Oct is already 00:00 on 2 Oct in Sri Lanka.
        var lateEveningUtc = new DateTime(2026, 10, 1, 18, 30, 0, DateTimeKind.Utc);
        var dueOn4Oct = new DateTime(2026, 10, 4, 3, 0, 0, DateTimeKind.Utc);

        Assert.Equal(3, ReturnFineCalculator.DaysUntilDue(dueOn4Oct, new DateTime(2026, 10, 1, 18, 29, 0, DateTimeKind.Utc)));
        Assert.Equal(2, ReturnFineCalculator.DaysUntilDue(dueOn4Oct, lateEveningUtc));
    }
}

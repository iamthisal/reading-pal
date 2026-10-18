using LendingService.Kafka;
using Microsoft.Extensions.Logging;
using Moq;

namespace LendingService.UnitTests;

public class DueDateReminderServiceTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 1)]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 0)]
    // Proves only loans due today, tomorrow or in two days receive reminders.
    public async Task QueueRemindersAsync_DueWindowBoundary_QueuesEligibleLoan(int days, int expected)
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var loan = LendingTestData.Loan(days);
        db.BorrowRecords.Add(loan);
        await db.SaveChangesAsync();
        var service = new DueDateReminderService(db, new Mock<ILogger<DueDateReminderService>>().Object);
        // Act
        var count = await service.QueueRemindersAsync(LendingTestData.Now, CancellationToken.None);
        // Assert
        Assert.Equal(expected, count);
        Assert.Equal(expected, db.ReservationEvents.Count());
        Assert.Equal(expected == 1, loan.DueReminderSentAtUtc.HasValue);
        if (expected == 1) Assert.Equal("book-due-soon", db.ReservationEvents.Single().EventType);
    }

    [Fact]
    // Proves a returned loan is excluded from reminders.
    public async Task QueueRemindersAsync_ReturnedLoan_QueuesNothing()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var loan = LendingTestData.Loan();
        loan.ReturnDate = LendingTestData.Now;
        db.BorrowRecords.Add(loan);
        await db.SaveChangesAsync();
        var service = new DueDateReminderService(db, new Mock<ILogger<DueDateReminderService>>().Object);
        // Act
        var count = await service.QueueRemindersAsync(LendingTestData.Now, CancellationToken.None);
        // Assert
        Assert.Equal(0, count);
        Assert.Equal(0, db.ReservationEvents.Count());
    }

    [Fact]
    // Proves repeating reminder processing never queues a second reminder for the loan.
    public async Task QueueRemindersAsync_AlreadyReminded_QueuesNothing()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.BorrowRecords.Add(LendingTestData.Loan());
        await db.SaveChangesAsync();
        var service = new DueDateReminderService(db, new Mock<ILogger<DueDateReminderService>>().Object);
        await service.QueueRemindersAsync(LendingTestData.Now, CancellationToken.None);
        // Act
        var count = await service.QueueRemindersAsync(LendingTestData.Now, CancellationToken.None);
        // Assert
        Assert.Equal(0, count);
        Assert.Equal(1, db.ReservationEvents.Count());
    }
}

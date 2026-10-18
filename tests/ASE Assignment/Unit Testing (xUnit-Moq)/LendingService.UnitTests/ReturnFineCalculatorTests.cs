using LendingService.Models;

namespace LendingService.UnitTests;

public class ReturnFineCalculatorTests
{
    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    // Proves early and same-day returns have no overdue days, while late returns do.
    public void DaysOverdue_ReturnDayBoundary_ReturnsNonnegativeDays(int dayOffset, int expected)
    {
        // Arrange
        var due = LendingTestData.Now;
        var returned = due.AddDays(dayOffset);
        // Act
        var days = ReturnFineCalculator.DaysOverdue(due, returned);
        // Assert
        Assert.Equal(expected, days);
        Assert.Equal(expected * 10m, days * ReturnFineCalculator.DailyRate);
    }

    [Fact]
    // Proves crossing midnight in Sri Lanka counts as one day even within the same UTC date.
    public void DaysOverdue_ColomboMidnight_CountsOneDay()
    {
        // Arrange
        var due = new DateTime(2026, 10, 18, 18, 29, 0, DateTimeKind.Utc);
        var returned = due.AddMinutes(2);
        // Act
        var days = ReturnFineCalculator.DaysOverdue(due, returned);
        // Assert
        Assert.Equal(1, days);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    // Proves days-until-due retains negative, zero and positive calendar differences.
    public void DaysUntilDue_DueDayBoundary_ReturnsSignedDays(int offset)
    {
        // Arrange
        var now = LendingTestData.Now;
        // Act
        var days = ReturnFineCalculator.DaysUntilDue(now.AddDays(offset), now);
        // Assert
        Assert.Equal(offset, days);
    }
}

using LendingService.Models;

namespace LendingService.Tests.Models;

public class ReturnFineCalculatorTests
{
    [Fact]
    public void DaysOverdue_WhenReturnedBeforeDueDate_IsZero()
    {
        var due = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);

        Assert.Equal(0, ReturnFineCalculator.DaysOverdue(due, due.AddHours(-1)));
    }

    [Fact]
    public void DaysOverdue_OnSameColomboCalendarDate_IsZero()
    {
        var due = new DateTime(2026, 9, 20, 0, 0, 0, DateTimeKind.Utc);
        var returned = new DateTime(2026, 9, 20, 17, 30, 0, DateTimeKind.Utc);

        Assert.Equal(0, ReturnFineCalculator.DaysOverdue(due, returned));
    }

    [Fact]
    public void DaysOverdue_WhenColomboDateCrossesMidnight_CountsOneDay()
    {
        var due = new DateTime(2026, 9, 20, 18, 29, 0, DateTimeKind.Utc);
        var returned = due.AddMinutes(2);

        Assert.Equal(1, ReturnFineCalculator.DaysOverdue(due, returned));
    }

    [Fact]
    public void DaysOverdue_CountsCalendarDaysRatherThanElapsedTwentyFourHourPeriods()
    {
        var due = new DateTime(2026, 9, 20, 18, 29, 0, DateTimeKind.Utc);
        var returned = new DateTime(2026, 9, 23, 0, 0, 0, DateTimeKind.Utc);

        Assert.Equal(3, ReturnFineCalculator.DaysOverdue(due, returned));
    }
}

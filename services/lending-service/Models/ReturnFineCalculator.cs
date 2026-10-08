namespace LendingService.Models;

public static class ReturnFineCalculator
{
    public const decimal DailyRate = 10m;
    private static readonly TimeZoneInfo LibraryTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    public static int DaysOverdue(DateTime dueDateUtc, DateTime returnDateUtc)
    {
        var dueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueDateUtc, DateTimeKind.Utc), LibraryTimeZone).Date;
        var returnDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(returnDateUtc, DateTimeKind.Utc), LibraryTimeZone).Date;
        return Math.Max(0, (returnDate - dueDate).Days);
    }

    /// <summary>
    /// Sri Lankan calendar days from today until the due date: 0 = due today, 1 = tomorrow,
    /// negative = overdue. Uses the same calendar as fines so reminders and fines agree on the day.
    /// </summary>
    public static int DaysUntilDue(DateTime dueDateUtc, DateTime nowUtc)
    {
        var dueDate = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(dueDateUtc, DateTimeKind.Utc), LibraryTimeZone).Date;
        var today = TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(nowUtc, DateTimeKind.Utc), LibraryTimeZone).Date;
        return (dueDate - today).Days;
    }
}

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
}

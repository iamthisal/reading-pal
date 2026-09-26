namespace LendingService.Models;

public sealed class Fine
{
    public int Id { get; set; }
    public int BorrowRecordId { get; set; }
    public int DaysOverdue { get; set; }
    public decimal DailyRate { get; set; } = 10m;
    public decimal Amount { get; set; }
    public string Status { get; set; } = "Unpaid";
}

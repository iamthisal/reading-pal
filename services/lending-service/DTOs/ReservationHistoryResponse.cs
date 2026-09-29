namespace LendingService.DTOs;

public sealed class ReservationHistoryResponse
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ReservationDate { get; set; }
    public DateTime? CheckoutDate { get; set; }
    public DateTime? DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public int? DaysOverdue { get; set; }
    public decimal? FineAmount { get; set; }
    public string? FineStatus { get; set; }
}

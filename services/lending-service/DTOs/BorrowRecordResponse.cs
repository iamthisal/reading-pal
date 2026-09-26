namespace LendingService.DTOs;

public class BorrowRecordResponse
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int UserId { get; set; }
    public int BookId { get; set; }
    public string UserName { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
    public DateTime CheckoutDate { get; set; }
    public DateTime DueDate { get; set; }
    public bool IsOverdue { get; set; }
}

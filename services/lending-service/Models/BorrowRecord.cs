namespace LendingService.Models;

public class BorrowRecord
{
    public int Id { get; set; }
    public int ReservationId { get; set; }
    public int BookId { get; set; }
    public int UserId { get; set; }
    public DateTime CheckoutDate { get; set; }
    public DateTime DueDate { get; set; }
    public DateTime? ReturnDate { get; set; }
    public DateTime? ReturnRequestedAtUtc { get; set; }
    // Set when the due-date reminder is queued, so each loan is reminded at most once.
    public DateTime? DueReminderSentAtUtc { get; set; }
}

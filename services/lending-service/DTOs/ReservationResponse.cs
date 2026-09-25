namespace LendingService.DTOs
{
    public class ReservationResponse
    {
        public int Id { get; set; }
        public int BookId { get; set; }
        public int UserId { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime ReservationDate { get; set; }
    }
}

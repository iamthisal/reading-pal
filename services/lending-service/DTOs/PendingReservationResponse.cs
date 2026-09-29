namespace LendingService.DTOs;

public class PendingReservationResponse : ReservationResponse
{
    public string UserName { get; set; } = string.Empty;
    public string BookTitle { get; set; } = string.Empty;
}

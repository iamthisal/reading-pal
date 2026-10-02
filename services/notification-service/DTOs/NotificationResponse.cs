namespace NotificationService.DTOs;

public sealed class NotificationResponse
{
    public int Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public int ReservationId { get; init; }
    public int BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public DateTime? DueDate { get; init; }
    public DateTime? ReturnDate { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

public sealed class UnreadCountResponse
{
    public int Count { get; init; }
}

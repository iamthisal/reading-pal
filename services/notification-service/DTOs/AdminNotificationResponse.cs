namespace NotificationService.DTOs;

public sealed class AdminNotificationResponse
{
    public const string PendingReservationsLink = "/admin/reservations/pending";
    public const string BookInventoryLink = "/admin/books";

    // Where the admin can follow up. A deleted book has no page, so its notification has no link.
    public static string? LinkFor(string type) => type switch
    {
        Models.NotificationTypes.AdminBookCreated or Models.NotificationTypes.AdminBookUpdated => BookInventoryLink,
        Models.NotificationTypes.AdminBookDeleted => null,
        _ => PendingReservationsLink
    };

    public int Id { get; init; }
    public string Type { get; init; } = string.Empty;
    public int ReservationId { get; init; }
    public int BookId { get; init; }
    public string BookTitle { get; init; } = string.Empty;
    public int CustomerUserId { get; init; }
    public string CustomerName { get; init; } = string.Empty;
    public DateTime? ReservationDate { get; init; }
    public string Message { get; init; } = string.Empty;
    // Where the admin acts on the request. The notification itself offers no actions, so an
    // outdated notification can only lead to the current pending list.
    public string? Link { get; init; } = PendingReservationsLink;
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}

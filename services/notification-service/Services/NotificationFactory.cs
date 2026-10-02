using System.Globalization;
using NotificationService.Kafka;
using NotificationService.Models;

namespace NotificationService.Services;

public static class NotificationFactory
{
    // Dates are shown as Sri Lankan calendar days, matching how Lending calculates due dates and fines.
    private static readonly TimeZoneInfo LibraryTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    public static string FallbackTitle(int bookId) => $"Book #{bookId}";

    /// <summary>Builds the notification for a Lending event, or returns null for event types users are not notified about.</summary>
    public static Notification? Create(LendingEvent evt, string? bookTitle)
    {
        var title = string.IsNullOrWhiteSpace(bookTitle) ? FallbackTitle(evt.BookId) : bookTitle.Trim();
        var (type, message) = evt.EventType switch
        {
            LendingEventTypes.ReservationAccepted => (NotificationTypes.ReservationAccepted,
                evt.DueDate is { } due
                    ? $"Your reservation for '{title}' was accepted. Please return it by {FormatDate(due)}."
                    : $"Your reservation for '{title}' was accepted."),
            LendingEventTypes.ReservationCancelled => (NotificationTypes.ReservationCancelled,
                string.Equals(evt.CancelledBy, "User", StringComparison.OrdinalIgnoreCase)
                    ? $"You cancelled your reservation for '{title}'."
                    : $"Your reservation for '{title}' was rejected by the library."),
            LendingEventTypes.BookReturned => (NotificationTypes.BookReturned,
                evt.ReturnDate is { } returned
                    ? $"'{title}' was returned on {FormatDate(returned)}. Thank you!"
                    : $"'{title}' was returned. Thank you!"),
            _ => (null, null)
        };
        if (type == null) return null;

        return new Notification
        {
            EventId = evt.EventId,
            UserId = evt.UserId,
            Type = type,
            ReservationId = evt.ReservationId,
            BookId = evt.BookId,
            BookTitle = title,
            Message = message!,
            DueDate = evt.DueDate is { } d ? AsUtc(d) : null,
            ReturnDate = evt.ReturnDate is { } r ? AsUtc(r) : null,
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    public static string FormatDate(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), LibraryTimeZone).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

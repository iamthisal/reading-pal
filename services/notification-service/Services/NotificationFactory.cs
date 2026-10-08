using System.Globalization;
using NotificationService.Kafka;
using NotificationService.Models;

namespace NotificationService.Services;

public static class NotificationFactory
{
    // Dates are shown as Sri Lankan calendar days, matching how Lending calculates due dates and fines.
    private static readonly TimeZoneInfo LibraryTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Asia/Colombo");

    /// <summary>
    /// Placeholder in admin messages, replaced with the customer's name when an admin reads the list:
    /// names come from the User Service, which only answers requests carrying an admin's token.
    /// </summary>
    public const string CustomerPlaceholder = "{customer}";

    public static string FallbackTitle(int bookId) => $"Book #{bookId}";
    public static string FallbackCustomerName(int userId) => $"Customer #{userId}";

    /// <summary>Builds the customer's notification for a Lending event, or null when the customer is not notified.</summary>
    public static Notification? CreateForUser(LendingEvent evt, string? bookTitle)
    {
        var title = Title(evt, bookTitle);
        var (type, message) = evt.EventType switch
        {
            LendingEventTypes.ReservationAccepted => (NotificationTypes.ReservationAccepted,
                evt.DueDate is { } due
                    ? $"Your reservation for '{title}' was accepted. Please return it by {FormatDate(due)}."
                    : $"Your reservation for '{title}' was accepted."),
            LendingEventTypes.ReservationCancelled => (NotificationTypes.ReservationCancelled,
                IsCancelledByCustomer(evt)
                    ? $"You cancelled your reservation for '{title}'."
                    : $"Your reservation for '{title}' was rejected by the library."),
            LendingEventTypes.BookReturned => (NotificationTypes.BookReturned,
                evt.ReturnDate is { } returned
                    ? $"'{title}' was returned on {FormatDate(returned)}. Thank you!"
                    : $"'{title}' was returned. Thank you!"),
            // Only an actual, positive fine is announced; estimated fines never produce this event.
            LendingEventTypes.FineRecorded when evt.Amount > 0 && evt.DaysOverdue > 0 => (NotificationTypes.FineRecorded,
                $"A fine of {FormatRupees(evt.Amount!.Value)} was recorded for '{title}': returned {evt.DaysOverdue} {(evt.DaysOverdue == 1 ? "day" : "days")} late."),
            LendingEventTypes.BookDueSoon when evt.DueDate is { } dueSoon => (NotificationTypes.DueDateReminder,
                $"Reminder: '{title}' is due {DueWhen(dueSoon, evt.DaysUntilDue)}. Please return it on time to avoid a fine."),
            _ => (null, null)
        };
        return type == null ? null : Build(evt, NotificationAudiences.User, type, title, message!);
    }

    /// <summary>
    /// Builds the admins' notification, or null. Admins hear only about customer actions that change the
    /// pending queue: a new reservation or a customer's own cancellation. Their own accepts and rejects
    /// produce nothing, so an admin action is never echoed back as a notification.
    /// </summary>
    public static Notification? CreateForAdmin(LendingEvent evt, string? bookTitle)
    {
        var title = Title(evt, bookTitle);
        var (type, message) = evt.EventType switch
        {
            LendingEventTypes.ReservationCreated => (NotificationTypes.NewReservation,
                evt.ReservationDate is { } reserved
                    ? $"{CustomerPlaceholder} reserved '{title}' on {FormatDateTime(reserved)}."
                    : $"{CustomerPlaceholder} reserved '{title}'."),
            LendingEventTypes.ReservationCancelled when IsCancelledByCustomer(evt) => (NotificationTypes.CustomerCancelledReservation,
                $"{CustomerPlaceholder} cancelled their pending reservation for '{title}'."),
            _ => (null, null)
        };
        return type == null ? null : Build(evt, NotificationAudiences.Admin, type, title, message!);
    }

    // A reminder normally arrives two days ahead; one created late (after missed checks) says "today"/"tomorrow".
    private static string DueWhen(DateTime dueUtc, int? daysUntilDue) => daysUntilDue switch
    {
        0 => $"today ({FormatDate(dueUtc)})",
        1 => $"tomorrow ({FormatDate(dueUtc)})",
        _ => $"on {FormatDate(dueUtc)}"
    };

    public static string FormatRupees(decimal amount) => $"Rs. {amount.ToString("N2", CultureInfo.InvariantCulture)}";

    public static string FormatDate(DateTime utc) =>
        ToLibraryTime(utc).ToString("d MMM yyyy", CultureInfo.InvariantCulture);

    public static string FormatDateTime(DateTime utc) =>
        ToLibraryTime(utc).ToString("d MMM yyyy 'at' h:mm tt", CultureInfo.InvariantCulture);

    private static bool IsCancelledByCustomer(LendingEvent evt) =>
        string.Equals(evt.CancelledBy, "User", StringComparison.OrdinalIgnoreCase);

    private static string Title(LendingEvent evt, string? bookTitle) =>
        string.IsNullOrWhiteSpace(bookTitle) ? FallbackTitle(evt.BookId) : bookTitle.Trim();

    private static Notification Build(LendingEvent evt, string audience, string type, string title, string message) => new()
    {
        EventId = evt.EventId,
        Audience = audience,
        UserId = evt.UserId,
        Type = type,
        ReservationId = evt.ReservationId,
        BookId = evt.BookId,
        BookTitle = title,
        Message = message,
        ReservationDate = evt.ReservationDate is { } reserved ? AsUtc(reserved) : null,
        DueDate = evt.DueDate is { } d ? AsUtc(d) : null,
        ReturnDate = evt.ReturnDate is { } r ? AsUtc(r) : null,
        CreatedAtUtc = DateTime.UtcNow
    };

    private static DateTime ToLibraryTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(AsUtc(utc), LibraryTimeZone);

    private static DateTime AsUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc ? value : value.Kind == DateTimeKind.Local ? value.ToUniversalTime() : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

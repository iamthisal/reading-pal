using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Tests.Services;

public class NotificationFactoryTests
{
    private static LendingEvent Event(string type, DateTime? dueDate = null, DateTime? returnDate = null, string? cancelledBy = null) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = type,
        SchemaVersion = 2,
        ReservationId = 5,
        UserId = 7,
        BookId = 12,
        DueDate = dueDate,
        ReturnDate = returnDate,
        CancelledBy = cancelledBy
    };

    [Fact]
    public void Create_ReservationAccepted_IncludesBookTitleAndDueDate()
    {
        // 18:30 UTC is already the next calendar day in Sri Lanka (UTC+5:30).
        var due = new DateTime(2026, 10, 13, 18, 30, 0, DateTimeKind.Utc);

        var notification = NotificationFactory.Create(Event(LendingEventTypes.ReservationAccepted, dueDate: due), "Clean Code")!;

        Assert.Equal(NotificationTypes.ReservationAccepted, notification.Type);
        Assert.Contains("'Clean Code'", notification.Message);
        Assert.Contains("14 Oct 2026", notification.Message);
        Assert.Equal(due, notification.DueDate);
        Assert.Equal(7, notification.UserId);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void Create_ReservationRejectedByAdmin_SaysRejected()
    {
        var notification = NotificationFactory.Create(Event(LendingEventTypes.ReservationCancelled, cancelledBy: "Admin"), "Clean Code")!;

        Assert.Equal(NotificationTypes.ReservationCancelled, notification.Type);
        Assert.Equal("Your reservation for 'Clean Code' was rejected by the library.", notification.Message);
    }

    [Fact]
    public void Create_ReservationCancelledByUser_ConfirmsTheirCancellation()
    {
        var notification = NotificationFactory.Create(Event(LendingEventTypes.ReservationCancelled, cancelledBy: "User"), "Clean Code")!;

        Assert.Equal("You cancelled your reservation for 'Clean Code'.", notification.Message);
    }

    [Fact]
    public void Create_BookReturned_IncludesTitleAndReturnDate()
    {
        var returned = new DateTime(2026, 9, 30, 4, 0, 0, DateTimeKind.Utc);

        var notification = NotificationFactory.Create(Event(LendingEventTypes.BookReturned, returnDate: returned), "Clean Code")!;

        Assert.Equal(NotificationTypes.BookReturned, notification.Type);
        Assert.Equal("'Clean Code' was returned on 30 Sep 2026. Thank you!", notification.Message);
        Assert.Equal(returned, notification.ReturnDate);
    }

    [Fact]
    public void Create_WithoutTitle_FallsBackToBookId()
    {
        var notification = NotificationFactory.Create(Event(LendingEventTypes.ReservationCancelled, cancelledBy: "Admin"), null)!;

        Assert.Equal("Book #12", notification.BookTitle);
        Assert.Contains("'Book #12'", notification.Message);
    }

    [Fact]
    public void Create_SchemaVersion1AcceptedEventWithoutDueDate_StillNotifies()
    {
        var notification = NotificationFactory.Create(Event(LendingEventTypes.ReservationAccepted), "Clean Code")!;

        Assert.Equal("Your reservation for 'Clean Code' was accepted.", notification.Message);
        Assert.Null(notification.DueDate);
    }

    [Fact]
    public void Create_UnknownEventType_ReturnsNull()
    {
        Assert.Null(NotificationFactory.Create(Event("book-created"), "Clean Code"));
    }
}

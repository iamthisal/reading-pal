using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.Tests.Services;

public class AdminNotificationFactoryTests
{
    private static LendingEvent Event(string type, string? cancelledBy = null, DateTime? reservationDate = null) => new()
    {
        EventId = Guid.NewGuid(),
        EventType = type,
        ReservationId = 5,
        UserId = 7,
        BookId = 12,
        ReservationDate = reservationDate,
        CancelledBy = cancelledBy
    };

    [Fact]
    public void CreateForAdmin_ReservationCreated_ShowsCustomerBookDateAndTime()
    {
        // 08:35 UTC is 2:05 PM in Sri Lanka (UTC+5:30).
        var reserved = new DateTime(2026, 10, 3, 8, 35, 0, DateTimeKind.Utc);

        var notification = NotificationFactory.CreateForAdmin(Event(LendingEventTypes.ReservationCreated, reservationDate: reserved), "Clean Code")!;

        Assert.Equal(NotificationAudiences.Admin, notification.Audience);
        Assert.Equal(NotificationTypes.NewReservation, notification.Type);
        Assert.Equal("{customer} reserved 'Clean Code' on 3 Oct 2026 at 2:05 PM.", notification.Message);
        Assert.Equal(7, notification.UserId);
        Assert.Equal(reserved, notification.ReservationDate);
        Assert.False(notification.IsRead);
    }

    [Fact]
    public void CreateForAdmin_CustomerCancelsPendingReservation_IdentifiesCustomerAndBook()
    {
        var notification = NotificationFactory.CreateForAdmin(Event(LendingEventTypes.ReservationCancelled, cancelledBy: "User"), "Clean Code")!;

        Assert.Equal(NotificationTypes.CustomerCancelledReservation, notification.Type);
        Assert.Equal("{customer} cancelled their pending reservation for 'Clean Code'.", notification.Message);
    }

    [Theory]
    [InlineData(LendingEventTypes.ReservationAccepted, null)]
    [InlineData(LendingEventTypes.ReservationCancelled, "Admin")]
    [InlineData(LendingEventTypes.BookReturned, null)]
    public void CreateForAdmin_AdminActionsAndReturns_CreateNoAdminNotification(string type, string? cancelledBy)
    {
        Assert.Null(NotificationFactory.CreateForAdmin(Event(type, cancelledBy), "Clean Code"));
    }

    [Fact]
    public void CreateForUser_ReservationCreated_DoesNotNotifyTheCustomer()
    {
        Assert.Null(NotificationFactory.CreateForUser(Event(LendingEventTypes.ReservationCreated), "Clean Code"));
    }
}

using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.UnitTests;

public class NotificationFactoryTests
{
    [Fact]
    // Proves accepted reservations create a user message with the local due date.
    public void CreateForUser_AcceptedReservation_IncludesTitleAndDueDate()
    {
        // Arrange
        var evt = NotificationTestData.Accepted();
        // Act
        var notification = NotificationFactory.CreateForUser(evt, " Dune ");
        // Assert
        Assert.Equal(NotificationAudiences.User, notification!.Audience);
        Assert.Equal("Your reservation for 'Dune' was accepted. Please return it by 20 Oct 2026.", notification.Message);
    }

    [Theory]
    [InlineData(-1, 1, false)]
    [InlineData(0, 1, false)]
    [InlineData(1, 0, false)]
    [InlineData(1, 1, true)]
    // Proves fine notices require both a positive amount and positive overdue days.
    public void CreateForUser_FineBoundary_NotifiesOnlyPositiveFine(int amount, int days, bool expected)
    {
        // Arrange
        var evt = new LendingEvent { EventType = LendingEventTypes.FineRecorded, BookId = 10, UserId = 5, Amount = amount, DaysOverdue = days };
        // Act
        var notification = NotificationFactory.CreateForUser(evt, "Dune");
        // Assert
        Assert.Equal(expected, notification != null);
        if (expected) Assert.Equal("A fine of Rs. 1.00 was recorded for 'Dune': returned 1 day late.", notification!.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    // Proves missing or blank titles fall back to the book ID.
    public void CreateForUser_MissingTitle_UsesBookId(string? title)
    {
        // Arrange
        var evt = NotificationTestData.Accepted();
        // Act
        var notification = NotificationFactory.CreateForUser(evt, title);
        // Assert
        Assert.Equal("Book #10", notification!.BookTitle);
    }

    [Theory]
    [InlineData("User", true)]
    [InlineData("user", true)]
    [InlineData("Admin", false)]
    [InlineData(null, false)]
    // Proves admins hear about customer cancellations but not their own rejections.
    public void CreateForAdmin_CancellationActor_NotifiesOnlyCustomerAction(string? actor, bool expected)
    {
        // Arrange
        var evt = new LendingEvent { EventType = LendingEventTypes.ReservationCancelled, CancelledBy = actor, UserId = 5, BookId = 10 };
        // Act
        var notification = NotificationFactory.CreateForAdmin(evt, "Dune");
        // Assert
        Assert.Equal(expected, notification != null);
    }

    [Fact]
    // Proves unsupported events do not produce notifications for either audience.
    public void CreateForUser_UnknownEvent_ReturnsNullForBothAudiences()
    {
        // Arrange
        var evt = new LendingEvent { EventType = "unknown", BookId = 10, UserId = 5 };
        // Act
        var customer = NotificationFactory.CreateForUser(evt, "Dune");
        var admin = NotificationFactory.CreateForAdmin(evt, "Dune");
        // Assert
        Assert.Equal<Notification?>(null, customer);
        Assert.Equal<Notification?>(null, admin);
    }
}

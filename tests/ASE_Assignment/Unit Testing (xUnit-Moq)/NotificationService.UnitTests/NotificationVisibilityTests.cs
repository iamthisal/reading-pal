using Microsoft.AspNetCore.Mvc;
using Moq;
using NotificationService.Controllers;
using NotificationService.DTOs;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.UnitTests;

public class NotificationVisibilityTests
{
    [Fact]
    // Proves a customer cannot mark another customer's notification as read.
    public async Task MarkRead_AnotherCustomersNotification_ReturnsNotFound()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        db.Notifications.Add(new Notification { Id = 1, EventId = Guid.NewGuid(), UserId = 6, Audience = NotificationAudiences.User });
        await db.SaveChangesAsync();
        var controller = new NotificationsController(db, new NotificationReadStore(db));
        NotificationTestData.SignIn(controller, "5");
        // Act
        var result = await controller.MarkRead(1, CancellationToken.None);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        Assert.False(db.Notifications.Single().IsRead);
    }

    [Fact]
    // Proves admins do not see notices about their own catalogue changes or request customer names.
    public async Task Get_AdminsOwnChange_HidesNotification()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        db.Notifications.Add(new Notification { EventId = Guid.NewGuid(), Audience = NotificationAudiences.Admin, PerformedBy = "admin-id" });
        await db.SaveChangesAsync();
        var customers = new Mock<ICustomerDirectory>();
        var controller = new AdminNotificationsController(db, customers.Object, new NotificationReadStore(db));
        NotificationTestData.SignIn(controller, "admin-id");
        // Act
        var result = await controller.Get();
        // Assert
        var items = (IEnumerable<AdminNotificationResponse>)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(0, items.Count());
        customers.Verify(directory => directory.GetNamesAsync(string.Empty, CancellationToken.None), Times.Never);
    }
}

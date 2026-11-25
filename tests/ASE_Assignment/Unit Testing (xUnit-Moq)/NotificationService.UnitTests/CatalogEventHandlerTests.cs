using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Services;

namespace NotificationService.UnitTests;

public class CatalogEventHandlerTests
{
    [Fact]
    // Proves a new book creates one customer announcement and one admin notice.
    public async Task HandleAsync_NewBook_CreatesTwoAudiences()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var handler = new CatalogEventHandler(db, new Mock<ILogger<CatalogEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.NewBook());
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Created, result);
        Assert.Equal(2, db.Notifications.Count());
        Assert.Equal(1, db.Notifications.Count(n => n.Audience == "Customers"));
        Assert.Equal(1, db.Notifications.Count(n => n.Audience == "Admin"));
    }

    [Fact]
    // Proves redelivering a catalogue event adds no extra notifications.
    public async Task HandleAsync_DuplicateBookEvent_KeepsTwoNotifications()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var handler = new CatalogEventHandler(db, new Mock<ILogger<CatalogEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.NewBook());
        await handler.HandleAsync(payload, CancellationToken.None);
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Duplicate, result);
        Assert.Equal(2, db.Notifications.Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{}")]
    [InlineData("invalid-json")]
    // Proves unusable catalogue messages are skipped without saving notifications.
    public async Task HandleAsync_InvalidCatalogPayload_SkipsEvent(string? payload)
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var handler = new CatalogEventHandler(db, new Mock<ILogger<CatalogEventHandler>>().Object);
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Skipped, result);
        Assert.Equal(0, db.Notifications.Count());
    }
}

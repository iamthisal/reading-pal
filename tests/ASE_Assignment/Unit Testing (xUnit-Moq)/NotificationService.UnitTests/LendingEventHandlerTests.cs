using System.Text.Json;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Kafka;
using NotificationService.Models;
using NotificationService.Services;

namespace NotificationService.UnitTests;

public class LendingEventHandlerTests
{
    [Fact]
    // Proves a delayed acceptance event cannot reopen a closed reservation.
    public async Task HandleAsync_ClosedReservation_KeepsClosedStatus()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        db.ReservationStates.Add(new ReservationState
        {
            ReservationId = 1, UserId = 5, BookId = 10, Status = ReservationStatuses.Closed
        });
        await db.SaveChangesAsync();
        var titles = new Mock<IBookTitleLookup>();
        titles.Setup(lookup => lookup.GetTitleAsync(10, CancellationToken.None)).ReturnsAsync("Dune");
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.Accepted());
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Created, result);
        Assert.Equal(ReservationStatuses.Closed, db.ReservationStates.Single().Status);
    }

    [Fact]
    // Proves an accepted event saves a notification and looks up its title exactly once.
    public async Task HandleAsync_AcceptedEvent_CreatesNotification()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var titles = new Mock<IBookTitleLookup>();
        titles.Setup(lookup => lookup.GetTitleAsync(10, CancellationToken.None)).ReturnsAsync("Dune");
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.Accepted());
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Created, result);
        Assert.Equal("Dune", db.Notifications.Single().BookTitle);
        titles.Verify(lookup => lookup.GetTitleAsync(10, CancellationToken.None), Times.Once);
    }

    [Fact]
    // Proves a repeated event creates no duplicate and makes no second title lookup.
    public async Task HandleAsync_DuplicateEvent_DoesNotLookUpTitleAgain()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var titles = new Mock<IBookTitleLookup>();
        titles.Setup(lookup => lookup.GetTitleAsync(10, CancellationToken.None)).ReturnsAsync("Dune");
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.Accepted());
        await handler.HandleAsync(payload, CancellationToken.None);
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Duplicate, result);
        Assert.Equal(1, db.Notifications.Count());
        titles.Verify(lookup => lookup.GetTitleAsync(10, CancellationToken.None), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("null")]
    [InlineData("not-json")]
    // Proves null, empty and malformed payloads are skipped before lookup or storage.
    public async Task HandleAsync_InvalidPayload_SkipsEvent(string? payload)
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var titles = new Mock<IBookTitleLookup>();
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        // Act
        var result = await handler.HandleAsync(payload, CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Skipped, result);
        Assert.Equal(0, db.Notifications.Count());
        titles.Verify(lookup => lookup.GetTitleAsync(10, CancellationToken.None), Times.Never);
    }

    [Fact]
    // Proves an unavailable title still creates a notification using a book-ID fallback.
    public async Task HandleAsync_TitleUnavailable_UsesFallbackTitle()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var titles = new Mock<IBookTitleLookup>();
        titles.Setup(lookup => lookup.GetTitleAsync(10, CancellationToken.None)).ReturnsAsync((string?)null);
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        // Act
        var result = await handler.HandleAsync(JsonSerializer.Serialize(NotificationTestData.Accepted()), CancellationToken.None);
        // Assert
        Assert.Equal(HandleOutcome.Created, result);
        Assert.Equal("Book #10", db.Notifications.Single().BookTitle);
        titles.Verify(lookup => lookup.GetTitleAsync(10, CancellationToken.None), Times.Once);
    }

    [Fact]
    // Proves a failed title lookup propagates so a later delivery can retry.
    public async Task HandleAsync_TitleLookupThrows_DoesNotSaveNotification()
    {
        // Arrange
        using var db = NotificationTestData.CreateDb();
        var titles = new Mock<IBookTitleLookup>();
        titles.Setup(lookup => lookup.GetTitleAsync(10, CancellationToken.None))
            .Returns(Task.FromException<string?>(new InvalidOperationException("Lookup failed")));
        var handler = new LendingEventHandler(db, titles.Object, new Mock<ILogger<LendingEventHandler>>().Object);
        var payload = JsonSerializer.Serialize(NotificationTestData.Accepted());
        // Act
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(payload, CancellationToken.None));
        // Assert
        Assert.Equal(0, db.Notifications.Count());
        titles.Verify(lookup => lookup.GetTitleAsync(10, CancellationToken.None), Times.Once);
    }
}

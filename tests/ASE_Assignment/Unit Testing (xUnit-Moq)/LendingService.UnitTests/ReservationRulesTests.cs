using LendingService.Controllers;
using LendingService.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;

namespace LendingService.UnitTests;

public class ReservationRulesTests
{
    [Fact]
    // Proves admin rejection cancels a pending reservation and queues its event without HTTP.
    public async Task Reject_PendingReservation_QueuesCancellation()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.Reservations.Add(LendingTestData.Reservation());
        await db.SaveChangesAsync();
        var clients = new Mock<IHttpClientFactory>();
        var controller = new ReservationsController(db, clients.Object, new Mock<IConfiguration>().Object);
        // Act
        var result = await controller.Reject(1, CancellationToken.None);
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.Equal("Cancelled", db.Reservations.Single().Status);
        Assert.Equal("reservation-cancelled", db.ReservationEvents.Single().EventType);
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    // Proves invalid and unknown reservation IDs fail before any HTTP client is requested.
    public async Task Accept_MissingReservation_ReturnsNotFound(int id)
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var clients = new Mock<IHttpClientFactory>();
        var controller = new ReservationsController(db, clients.Object, new Mock<IConfiguration>().Object);
        // Act
        var result = await controller.Accept(id, CancellationToken.None);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }

    [Theory]
    [InlineData(null, 401)]
    [InlineData("", 401)]
    [InlineData("invalid", 401)]
    [InlineData("admin-id", 400)]
    // Proves missing, malformed and admin identities cannot reserve books.
    public async Task ReserveBook_InvalidIdentity_RejectsBeforeHttp(string? identity, int expectedStatus)
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var clients = new Mock<IHttpClientFactory>();
        var controller = new ReservationsController(db, clients.Object, new Mock<IConfiguration>().Object);
        LendingTestData.SignIn(controller, identity);
        // Act
        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 10 });
        // Assert
        Assert.Equal(expectedStatus, ((ObjectResult)result.Result!).StatusCode);
        Assert.Equal(0, db.Reservations.Count());
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }

    [Fact]
    // Proves a reader can cancel their pending reservation and queue one cancellation event.
    public async Task Cancel_OwnPendingReservation_QueuesCancellation()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.Reservations.Add(LendingTestData.Reservation());
        await db.SaveChangesAsync();
        var clients = new Mock<IHttpClientFactory>();
        var controller = new MyBorrowingsController(db, clients.Object, new Mock<IConfiguration>().Object);
        LendingTestData.SignIn(controller, "5");
        // Act
        var result = await controller.Cancel(1, CancellationToken.None);
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.Equal("Cancelled", db.Reservations.Single().Status);
        Assert.Equal(1, db.ReservationEvents.Count());
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }

    [Fact]
    // Proves a reader cannot cancel another reader's reservation.
    public async Task Cancel_AnotherReadersReservation_ReturnsNotFound()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        db.Reservations.Add(LendingTestData.Reservation());
        await db.SaveChangesAsync();
        var controller = new MyBorrowingsController(db, new Mock<IHttpClientFactory>().Object, new Mock<IConfiguration>().Object);
        LendingTestData.SignIn(controller, "6");
        // Act
        var result = await controller.Cancel(1, CancellationToken.None);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        Assert.Equal("Pending", db.Reservations.Single().Status);
        Assert.Equal(0, db.ReservationEvents.Count());
    }

    [Fact]
    // Proves returning a missing loan does not contact Inventory.
    public async Task Return_MissingLoan_ReturnsNotFound()
    {
        // Arrange
        using var db = LendingTestData.CreateDb();
        var clients = new Mock<IHttpClientFactory>();
        var controller = new ReturnsController(db, clients.Object, new Mock<IConfiguration>().Object);
        // Act
        var result = await controller.Return(99, CancellationToken.None);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }
}

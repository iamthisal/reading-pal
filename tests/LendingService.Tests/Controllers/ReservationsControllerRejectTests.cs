using LendingService.Controllers;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerRejectTests
{
    private static ReservationsController CreateController(LendingService.Data.LendingDbContext context)
    {
        var controller = new ReservationsController(
            context,
            new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
                throw new InvalidOperationException("Reject should not call downstream services."))),
            ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);
        return controller;
    }

    [Fact]
    public async Task Reject_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context);

        var result = await controller.Reject(123, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Theory]
    [InlineData("Accepting")]
    [InlineData("Borrowed")]
    [InlineData("Cancelled")]
    public async Task Reject_WhenReservationIsNotPending_ReturnsConflict(string status)
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = status });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Reject(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Reject_WhenReservationIsPending_CancelsItAndRecordsEvent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Reject(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var reservation = await context.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Cancelled", reservation.Status);
        Assert.Single(context.ReservationEvents);
        Assert.Empty(context.BorrowRecords);
    }
}

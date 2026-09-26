using LendingService.Controllers;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerAcceptTests
{
    private static ReservationsController CreateController(LendingService.Data.LendingDbContext context) =>
        new(context, new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("Accept should not call downstream services on this branch."))),
            ControllerTestFactory.CreateConfiguration());

    [Fact]
    public async Task Accept_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context);

        var result = await controller.Accept(123, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Accept_WhenReservationIsNotPending_ReturnsConflict()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Accept(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Accept_WhenReservationIsPending_CreatesBorrowRecordAndMarksBorrowed()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context);

        var result = await controller.Accept(1, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);

        var reservation = await context.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Borrowed", reservation.Status);
        Assert.NotNull(reservation.CheckoutDate);
        Assert.Equal(reservation.CheckoutDate!.Value.AddDays(14), reservation.DueDate);

        var borrowRecord = await context.BorrowRecords.SingleAsync(b => b.ReservationId == 1);
        Assert.Equal(10, borrowRecord.BookId);
        Assert.Equal(20, borrowRecord.UserId);
        Assert.Equal(reservation.DueDate, borrowRecord.DueDate);

        Assert.Single(context.ReservationEvents);
    }
}

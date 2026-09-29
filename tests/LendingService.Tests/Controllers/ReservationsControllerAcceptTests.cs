using LendingService.Controllers;
using LendingService.Data;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerAcceptTests
{
    private static ReservationsController CreateController(
        LendingDbContext context,
        FakeHttpMessageHandler handler,
        string? inventoryBaseUrl = "http://inventory-service.test")
    {
        var controller = new ReservationsController(
            context,
            new FakeHttpClientFactory(handler),
            ControllerTestFactory.CreateConfiguration(inventoryServiceBaseUrl: inventoryBaseUrl));
        ControllerTestFactory.AttachHttpContext(controller);
        return controller;
    }

    private static FakeHttpMessageHandler NoHttpCallsExpected() => new(_ =>
        throw new InvalidOperationException("No HTTP call expected."));

    [Fact]
    public async Task Accept_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context, NoHttpCallsExpected());

        var result = await controller.Accept(123, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Accept_WhenReservationIsAlreadyProcessed_ReturnsConflict()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, NoHttpCallsExpected());

        var result = await controller.Accept(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Accept_WhenInventoryConfigurationMissing_Returns500()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, NoHttpCallsExpected(), inventoryBaseUrl: null);

        var result = await controller.Accept(1, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task Accept_MovesPendingReservationToAcceptingBeforeCallingInventory()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler(request =>
        {
            Assert.Contains("/checkouts/1", request.RequestUri!.AbsolutePath);
            return JsonResponse.Conflict();
        });
        var controller = CreateController(context, handler);

        var result = await controller.Accept(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        var reservation = await context.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Accepting", reservation.Status);
    }

    [Fact]
    public async Task Accept_WhenInventoryCannotAllocateACopy_ReturnsConflictAndKeepsAccepting()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Accepting" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Conflict()));

        var result = await controller.Accept(1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
        var reservation = await context.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Accepting", reservation.Status);
        Assert.Empty(context.BorrowRecords);
    }

    [Fact]
    public async Task Accept_WhenInventoryServiceUnreachable_Returns503()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, FakeHttpMessageHandler.ThrowingRequestException());

        var result = await controller.Accept(1, CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task Accept_WhenInventoryCheckoutSucceeds_CreatesBorrowRecordUsingInventoryCheckoutDate()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();

        var checkoutDate = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        var controller = CreateController(context, new FakeHttpMessageHandler(_ =>
            JsonResponse.Ok(new { CheckoutDateUtc = checkoutDate })));

        var result = await controller.Accept(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        var reservation = await context.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Borrowed", reservation.Status);
        Assert.Equal(checkoutDate, reservation.CheckoutDate);
        Assert.Equal(checkoutDate.AddDays(14), reservation.DueDate);

        var borrowRecord = await context.BorrowRecords.SingleAsync(b => b.ReservationId == 1);
        Assert.Equal(checkoutDate, borrowRecord.CheckoutDate);
        Assert.Single(context.ReservationEvents);
    }
}

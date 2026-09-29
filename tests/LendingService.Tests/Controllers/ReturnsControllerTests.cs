using LendingService.Controllers;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class ReturnsControllerTests
{
    private static ReturnsController CreateController(
        LendingService.Data.LendingDbContext context,
        HttpMessageHandler handler,
        string? inventoryUrl = "http://inventory.test")
    {
        var controller = new ReturnsController(
            context,
            new FakeHttpClientFactory(handler),
            ControllerTestFactory.CreateConfiguration(inventoryServiceBaseUrl: inventoryUrl));
        ControllerTestFactory.AttachHttpContext(controller);
        return controller;
    }

    private static async Task SeedLoan(
        LendingService.Data.LendingDbContext context,
        string status = "Borrowed",
        DateTime? dueDate = null)
    {
        context.Reservations.Add(new Reservation
        {
            Id = 10,
            BookId = 20,
            UserId = 30,
            Status = status,
            ReservationDate = DateTime.UtcNow.AddDays(-20)
        });
        context.BorrowRecords.Add(new BorrowRecord
        {
            Id = 40,
            ReservationId = 10,
            BookId = 20,
            UserId = 30,
            CheckoutDate = DateTime.UtcNow.AddDays(-15),
            DueDate = dueDate ?? DateTime.UtcNow.AddDays(1)
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task Return_WhenBorrowRecordDoesNotExist_ReturnsNotFound()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        var result = await controller.Return(999, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Return_WhenReservationIsNotBorrowed_ReturnsConflict()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context, status: "Cancelled");
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        var result = await controller.Return(40, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Return_WithoutInventoryConfiguration_Returns500WithoutChangingLoan()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context);
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })), inventoryUrl: null);

        var result = Assert.IsType<ObjectResult>(await controller.Return(40, CancellationToken.None));

        Assert.Equal(500, result.StatusCode);
        Assert.Equal("Borrowed", (await context.Reservations.FindAsync(10))!.Status);
        Assert.Null((await context.BorrowRecords.FindAsync(40))!.ReturnRequestedAtUtc);
    }

    [Fact]
    public async Task Return_WhenInventoryIsUnavailable_PreservesOriginalReturnRequestForRetry()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context);
        var controller = CreateController(context, FakeHttpMessageHandler.ThrowingRequestException());

        var result = Assert.IsType<ObjectResult>(await controller.Return(40, CancellationToken.None));

        Assert.Equal(503, result.StatusCode);
        Assert.Equal("Returning", (await context.Reservations.FindAsync(10))!.Status);
        Assert.NotNull((await context.BorrowRecords.FindAsync(40))!.ReturnRequestedAtUtc);
    }

    [Fact]
    public async Task Return_WhenOnTime_CompletesLoanWithoutFineAndQueuesEvent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context, dueDate: DateTime.UtcNow.AddDays(1));
        HttpRequestMessage? sent = null;
        var controller = CreateController(context, new FakeHttpMessageHandler(request =>
        {
            sent = request;
            return JsonResponse.Ok(new { });
        }));

        var result = await controller.Return(40, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("/api/Books/20/checkouts/10/return", sent!.RequestUri!.AbsolutePath);
        Assert.Equal("Returned", (await context.Reservations.FindAsync(10))!.Status);
        Assert.NotNull((await context.BorrowRecords.FindAsync(40))!.ReturnDate);
        Assert.Empty(context.Fines);
        var message = Assert.Single(context.ReservationEvents);
        Assert.Equal("book-returned", message.EventType);
        Assert.Contains("book-returned", message.Payload);
    }

    [Fact]
    public async Task Return_WhenOverdue_PersistsCalculatedUnpaidFine()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context, dueDate: DateTime.UtcNow.AddDays(-3));
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        Assert.IsType<OkObjectResult>(await controller.Return(40, CancellationToken.None));

        var loan = (await context.BorrowRecords.FindAsync(40))!;
        var fine = Assert.Single(context.Fines);
        var expectedDays = ReturnFineCalculator.DaysOverdue(loan.DueDate, loan.ReturnDate!.Value);
        Assert.Equal(expectedDays, fine.DaysOverdue);
        Assert.Equal(expectedDays * ReturnFineCalculator.DailyRate, fine.Amount);
        Assert.Equal("Unpaid", fine.Status);
    }

    [Fact]
    public async Task Return_WhenCompletedRequestIsRetried_DoesNotCallInventoryOrCreateDuplicateRecords()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        await SeedLoan(context, dueDate: DateTime.UtcNow.AddDays(-2));
        var calls = 0;
        var controller = CreateController(context, new FakeHttpMessageHandler(_ =>
        {
            calls++;
            return JsonResponse.Ok(new { });
        }));

        Assert.IsType<OkObjectResult>(await controller.Return(40, CancellationToken.None));
        Assert.IsType<OkObjectResult>(await controller.Return(40, CancellationToken.None));

        Assert.Equal(1, calls);
        Assert.Single(context.Fines);
        Assert.Single(context.ReservationEvents);
    }
}

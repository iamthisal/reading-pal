using System.Text.Json;
using LendingService.Controllers;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class MyBorrowingsControllerTests
{
    private static MyBorrowingsController CreateController(
        LendingService.Data.LendingDbContext context,
        HttpMessageHandler handler,
        int? userId = 7,
        string? inventoryUrl = "http://inventory.test")
    {
        var controller = new MyBorrowingsController(
            context,
            new FakeHttpClientFactory(handler),
            ControllerTestFactory.CreateConfiguration(inventoryServiceBaseUrl: inventoryUrl));
        if (userId.HasValue) ControllerTestFactory.AttachUser(controller, userId.Value);
        else ControllerTestFactory.AttachHttpContext(controller);
        return controller;
    }

    private static JsonDocument Body(OkObjectResult result) =>
        JsonSerializer.SerializeToDocument(result.Value, new JsonSerializerOptions(JsonSerializerDefaults.Web));

    [Fact]
    public async Task Get_WithInvalidIdentity_ReturnsUnauthorized()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })), userId: null);

        var result = await controller.Get(CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Get_ReturnsOnlyCurrentUsersGroupedLoansReservationsAndFineTotals()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.AddRange(
            new Reservation { Id = 1, UserId = 7, BookId = 101, Status = "Borrowed" },
            new Reservation { Id = 2, UserId = 7, BookId = 102, Status = "Returned" },
            new Reservation { Id = 3, UserId = 7, BookId = 103, Status = "Pending" },
            new Reservation { Id = 4, UserId = 7, BookId = 104, Status = "Cancelled" },
            new Reservation { Id = 5, UserId = 99, BookId = 105, Status = "Borrowed" });
        context.BorrowRecords.AddRange(
            new BorrowRecord { Id = 11, ReservationId = 1, UserId = 7, BookId = 101,
                CheckoutDate = DateTime.UtcNow.AddDays(-20), DueDate = DateTime.UtcNow.AddDays(-2) },
            new BorrowRecord { Id = 12, ReservationId = 2, UserId = 7, BookId = 102,
                CheckoutDate = DateTime.UtcNow.AddDays(-30), DueDate = DateTime.UtcNow.AddDays(-10), ReturnDate = DateTime.UtcNow.AddDays(-7) },
            new BorrowRecord { Id = 15, ReservationId = 5, UserId = 99, BookId = 105,
                CheckoutDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(14) });
        context.Fines.Add(new Fine { Id = 1, BorrowRecordId = 12, DaysOverdue = 3, Amount = 30m, DailyRate = 10m });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(request =>
        {
            var id = request.RequestUri!.Segments.Last();
            return JsonResponse.Ok(new { Title = $"Title {id}" });
        }));

        using var body = Body(Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None)));
        var root = body.RootElement;

        Assert.Single(root.GetProperty("active").EnumerateArray());
        Assert.Single(root.GetProperty("history").EnumerateArray());
        Assert.Single(root.GetProperty("pending").EnumerateArray());
        Assert.Single(root.GetProperty("cancelled").EnumerateArray());
        Assert.Equal(30m, root.GetProperty("totalUnpaid").GetDecimal());
        Assert.True(root.GetProperty("estimatedActiveFines").GetDecimal() > 0m);
        Assert.Equal("Title 101", root.GetProperty("active")[0].GetProperty("bookTitle").GetString());
    }

    [Fact]
    public async Task Get_WhenInventoryIsUnavailable_StillReturnsFinancialRecordsWithPlaceholderTitle()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 101, Status = "Returned" });
        context.BorrowRecords.Add(new BorrowRecord { Id = 11, ReservationId = 1, UserId = 7, BookId = 101,
            CheckoutDate = DateTime.UtcNow.AddDays(-5), DueDate = DateTime.UtcNow.AddDays(-2), ReturnDate = DateTime.UtcNow });
        context.Fines.Add(new Fine { Id = 1, BorrowRecordId = 11, DaysOverdue = 2, Amount = 20m, DailyRate = 10m });
        await context.SaveChangesAsync();
        var controller = CreateController(context, FakeHttpMessageHandler.ThrowingRequestException());

        using var body = Body(Assert.IsType<OkObjectResult>(await controller.Get(CancellationToken.None)));

        var history = Assert.Single(body.RootElement.GetProperty("history").EnumerateArray());
        Assert.Contains("title unavailable", history.GetProperty("bookTitle").GetString());
        Assert.Equal(20m, body.RootElement.GetProperty("totalUnpaid").GetDecimal());
    }

    [Fact]
    public async Task Cancel_PendingReservationOwnedByUser_CancelsAndQueuesEvent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 101, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        var result = await controller.Cancel(1, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        Assert.Equal("Cancelled", (await context.Reservations.FindAsync(1))!.Status);
        var message = Assert.Single(context.ReservationEvents);
        Assert.Equal("reservation-cancelled", message.EventType);
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal("User", payload.RootElement.GetProperty("cancelledBy").GetString());
    }

    [Fact]
    public async Task Cancel_WithInvalidIdentity_ReturnsUnauthorized()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })), userId: null);

        var result = await controller.Cancel(1, CancellationToken.None);

        Assert.IsType<UnauthorizedObjectResult>(result);
    }

    [Fact]
    public async Task Cancel_ReservationOwnedByAnotherUser_ReturnsNotFound()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 8, BookId = 101, Status = "Pending" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        Assert.IsType<NotFoundObjectResult>(await controller.Cancel(1, CancellationToken.None));
        Assert.Equal("Pending", (await context.Reservations.FindAsync(1))!.Status);
    }

    [Theory]
    [InlineData("Accepting")]
    [InlineData("Borrowed")]
    [InlineData("Returned")]
    public async Task Cancel_WhenReservationIsNoLongerPending_ReturnsConflict(string status)
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 101, Status = status });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        Assert.IsType<ConflictObjectResult>(await controller.Cancel(1, CancellationToken.None));
        Assert.Empty(context.ReservationEvents);
    }

    [Fact]
    public async Task Cancel_WhenAlreadyCancelled_IsIdempotent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 101, Status = "Cancelled" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { })));

        Assert.IsType<OkObjectResult>(await controller.Cancel(1, CancellationToken.None));
        Assert.Empty(context.ReservationEvents);
    }
}

using LendingService.Controllers;
using LendingService.DTOs;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerGetHistoryTests
{
    private static ReservationsController CreateController(LendingService.Data.LendingDbContext context, HttpMessageHandler handler)
    {
        var controller = new ReservationsController(
            context,
            new FakeHttpClientFactory(handler),
            ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);
        return controller;
    }

    private static FakeHttpMessageHandler SuccessfulLookups() => new(request =>
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.Contains("/api/admin/users/active"))
            return JsonResponse.Ok(new[] { new { Id = 7, FirstName = "Jane", LastName = "Doe" } });
        if (path.Contains("/api/admin/users/pending")) return JsonResponse.Ok(Array.Empty<object>());
        if (path.Contains("/api/Books/"))
            return JsonResponse.Ok(new { Title = $"Book {path.Split('/').Last()}" });
        throw new InvalidOperationException($"Unexpected request to {path}");
    });

    [Fact]
    public async Task GetHistory_WithNoCompletedReservations_ReturnsEmptyResultWithoutLookups()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 10, Status = "Borrowed" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No lookup should occur.")));

        var result = await controller.GetHistory(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<ReservationHistoryResponse>>(ok.Value));
    }

    [Fact]
    public async Task GetHistory_ReturnsCancelledAndReturnedReservationsWithSavedFineDetails()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var returnedAt = new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc);
        context.Reservations.AddRange(
            new Reservation { Id = 1, UserId = 7, BookId = 10, Status = "Cancelled", ReservationDate = returnedAt.AddDays(-1) },
            new Reservation { Id = 2, UserId = 7, BookId = 11, Status = "Returned", ReservationDate = returnedAt.AddDays(-2), ReturnDate = returnedAt },
            new Reservation { Id = 3, UserId = 7, BookId = 12, Status = "Borrowed", ReservationDate = returnedAt });
        context.BorrowRecords.Add(new BorrowRecord { Id = 20, ReservationId = 2, UserId = 7, BookId = 11,
            CheckoutDate = returnedAt.AddDays(-20), DueDate = returnedAt.AddDays(-3), ReturnDate = returnedAt });
        context.Fines.Add(new Fine { Id = 30, BorrowRecordId = 20, DaysOverdue = 3, DailyRate = 10m, Amount = 30m, Status = "Unpaid" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, SuccessfulLookups());

        var result = await controller.GetHistory(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var records = Assert.IsAssignableFrom<IEnumerable<ReservationHistoryResponse>>(ok.Value).ToList();
        Assert.Equal(2, records.Count);
        var cancelled = records.Single(r => r.Status == "Cancelled");
        Assert.Null(cancelled.DaysOverdue);
        Assert.Null(cancelled.FineAmount);
        var returned = records.Single(r => r.Status == "Returned");
        Assert.Equal("Jane Doe", returned.UserName);
        Assert.Equal("Book 11", returned.BookTitle);
        Assert.Equal(3, returned.DaysOverdue);
        Assert.Equal(30m, returned.FineAmount);
        Assert.Equal("Unpaid", returned.FineStatus);
        Assert.Equal(DateTimeKind.Utc, returned.ReturnDate!.Value.Kind);
    }

    [Fact]
    public async Task GetHistory_WhenLookupServiceFails_Returns503()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, UserId = 7, BookId = 10, Status = "Cancelled" });
        await context.SaveChangesAsync();
        var controller = CreateController(context, FakeHttpMessageHandler.ThrowingRequestException());

        var result = await controller.GetHistory(CancellationToken.None);

        var error = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, error.StatusCode);
    }
}

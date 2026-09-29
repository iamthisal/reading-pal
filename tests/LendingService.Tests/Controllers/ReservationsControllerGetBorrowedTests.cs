using LendingService.Controllers;
using LendingService.DTOs;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerGetBorrowedTests
{
    [Fact]
    public async Task GetBorrowed_WithNoOutstandingRecords_ReturnsEmptyOkResult()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = new ReservationsController(context, new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call should be made when there are no borrowed records."))),
            ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetBorrowed(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<BorrowRecordResponse>>(ok.Value));
    }

    [Fact]
    public async Task GetBorrowed_ExcludesRecordsThatHaveBeenReturned()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
        context.BorrowRecords.Add(new BorrowRecord
        {
            Id = 1,
            ReservationId = 1,
            BookId = 10,
            UserId = 20,
            CheckoutDate = DateTime.UtcNow.AddDays(-20),
            DueDate = DateTime.UtcNow.AddDays(-6),
            ReturnDate = DateTime.UtcNow.AddDays(-5)
        });
        await context.SaveChangesAsync();
        var controller = new ReservationsController(context, new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call should be made when there are no borrowed records."))),
            ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetBorrowed(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        Assert.Empty(Assert.IsAssignableFrom<IEnumerable<BorrowRecordResponse>>(ok.Value));
    }

    [Fact]
    public async Task GetBorrowed_FlagsRecordsPastDueDateAsOverdue()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
        context.Reservations.Add(new Reservation { Id = 2, BookId = 11, UserId = 21, Status = "Borrowed" });
        context.BorrowRecords.Add(new BorrowRecord
        {
            Id = 1,
            ReservationId = 1,
            BookId = 10,
            UserId = 20,
            CheckoutDate = DateTime.UtcNow.AddDays(-20),
            DueDate = DateTime.UtcNow.AddDays(-6)
        });
        context.BorrowRecords.Add(new BorrowRecord
        {
            Id = 2,
            ReservationId = 2,
            BookId = 11,
            UserId = 21,
            CheckoutDate = DateTime.UtcNow.AddDays(-1),
            DueDate = DateTime.UtcNow.AddDays(13)
        });
        await context.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/api/admin/users/"))
                return JsonResponse.Ok(Array.Empty<object>());
            if (path.Contains("/api/Books/"))
                return JsonResponse.NotFound();
            throw new InvalidOperationException($"Unexpected request to {path}");
        });
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetBorrowed(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var records = Assert.IsAssignableFrom<IEnumerable<BorrowRecordResponse>>(ok.Value).ToList();
        Assert.Equal(2, records.Count);
        Assert.True(records.Single(r => r.Id == 1).IsOverdue);
        Assert.False(records.Single(r => r.Id == 2).IsOverdue);
    }

    [Fact]
    public async Task GetBorrowed_WhenReturnIsPending_FreezesFineAtOriginalRequestDate()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var dueDate = new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc);
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Returning" });
        context.BorrowRecords.Add(new BorrowRecord
        {
            Id = 1,
            ReservationId = 1,
            BookId = 10,
            UserId = 20,
            CheckoutDate = dueDate.AddDays(-14),
            DueDate = dueDate,
            ReturnRequestedAtUtc = dueDate.AddDays(2)
        });
        await context.SaveChangesAsync();
        var handler = new FakeHttpMessageHandler(request => request.RequestUri!.AbsolutePath.Contains("/api/admin/users/")
            ? JsonResponse.Ok(Array.Empty<object>())
            : JsonResponse.NotFound());
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetBorrowed(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var record = Assert.Single(Assert.IsAssignableFrom<IEnumerable<BorrowRecordResponse>>(ok.Value));
        Assert.True(record.IsReturning);
        Assert.True(record.IsOverdue);
        Assert.Equal(2, record.DaysOverdue);
        Assert.Equal(20m, record.FineAmount);
    }
}

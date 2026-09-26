using LendingService.Controllers;
using LendingService.DTOs;
using LendingService.Models;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerGetPendingTests
{
    [Fact]
    public async Task GetPending_WithNoPendingReservations_ReturnsEmptyOkResult()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var config = ControllerTestFactory.CreateConfiguration();
        var factory = new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call should be made when there are no pending reservations.")));
        var controller = new ReservationsController(context, factory, config);
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IEnumerable<PendingReservationResponse>>(ok.Value);
        Assert.Empty(body);
    }

    [Fact]
    public async Task GetPending_IncludesReservationsStuckInAcceptingState()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Accepting" });
        await context.SaveChangesAsync();

        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/api/admin/users/"))
                return JsonResponse.Ok(Array.Empty<object>());
            if (path.Contains("/api/Books/10"))
                return JsonResponse.Ok(new { Title = "Clean Code" });
            throw new InvalidOperationException($"Unexpected request to {path}");
        });
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), ControllerTestFactory.CreateConfiguration());
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var reservation = Assert.Single(Assert.IsAssignableFrom<IEnumerable<PendingReservationResponse>>(ok.Value));
        Assert.Equal("Accepting", reservation.Status);
    }

    [Fact]
    public async Task GetPending_WhenServiceUrlsAreNotConfigured_Returns500()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();

        var config = ControllerTestFactory.CreateConfiguration(userServiceBaseUrl: null, inventoryServiceBaseUrl: null);
        var factory = new FakeHttpClientFactory(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call should be made when configuration is missing.")));
        var controller = new ReservationsController(context, factory, config);
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task GetPending_WithPendingReservations_MergesUserNameAndBookTitle()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation
        {
            Id = 1,
            BookId = 10,
            UserId = 20,
            Status = "Pending",
            ReservationDate = new DateTime(2026, 9, 1, 0, 0, 0, DateTimeKind.Unspecified)
        });
        await context.SaveChangesAsync();

        var config = ControllerTestFactory.CreateConfiguration();
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/api/admin/users/active"))
                return JsonResponse.Ok(new[] { new { Id = 20, FirstName = "Jane", LastName = "Doe" } });
            if (path.Contains("/api/admin/users/pending"))
                return JsonResponse.Ok(Array.Empty<object>());
            if (path.Contains("/api/Books/10"))
                return JsonResponse.Ok(new { Title = "Clean Code" });
            throw new InvalidOperationException($"Unexpected request to {path}");
        });
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), config);
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var body = Assert.IsAssignableFrom<IEnumerable<PendingReservationResponse>>(ok.Value).ToList();
        var reservation = Assert.Single(body);
        Assert.Equal("Jane Doe", reservation.UserName);
        Assert.Equal("Clean Code", reservation.BookTitle);
        Assert.Equal("Pending", reservation.Status);
        Assert.Equal(DateTimeKind.Utc, reservation.ReservationDate.Kind);
    }

    [Fact]
    public async Task GetPending_WhenBookIsMissingFromInventory_UsesDeletedBookPlaceholder()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 99, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();

        var config = ControllerTestFactory.CreateConfiguration();
        var handler = new FakeHttpMessageHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            if (path.Contains("/api/admin/users/"))
                return JsonResponse.Ok(Array.Empty<object>());
            if (path.Contains("/api/Books/99"))
                return JsonResponse.NotFound();
            throw new InvalidOperationException($"Unexpected request to {path}");
        });
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), config);
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result.Result);
        var reservation = Assert.Single(Assert.IsAssignableFrom<IEnumerable<PendingReservationResponse>>(ok.Value));
        Assert.Equal("Deleted book (#99)", reservation.BookTitle);
        Assert.Equal("User unavailable (#20)", reservation.UserName);
    }

    [Fact]
    public async Task GetPending_WhenDownstreamServiceFails_Returns503()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        context.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
        await context.SaveChangesAsync();

        var config = ControllerTestFactory.CreateConfiguration();
        var controller = new ReservationsController(context, new FakeHttpClientFactory(FakeHttpMessageHandler.ThrowingRequestException()), config);
        ControllerTestFactory.AttachHttpContext(controller);

        var result = await controller.GetPending(CancellationToken.None);

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, statusResult.StatusCode);
    }
}

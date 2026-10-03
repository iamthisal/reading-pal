using System.Text.Json;
using System.Security.Claims;
using LendingService.Controllers;
using LendingService.DTOs;
using LendingService.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace LendingService.Tests.Controllers;

public class ReservationsControllerReserveBookTests
{
    private static ReservationsController CreateController(
        FakeHttpMessageHandler handler,
        string? userId = "20",
        string? inventoryBaseUrl = "http://inventory-service.test",
        LendingService.Data.LendingDbContext? context = null)
    {
        context ??= ControllerTestFactory.CreateDbContext();
        var config = ControllerTestFactory.CreateConfiguration(inventoryServiceBaseUrl: inventoryBaseUrl);
        var controller = new ReservationsController(context, new FakeHttpClientFactory(handler), config);

        var claims = new List<Claim>();
        if (userId != null)
            claims.Add(new Claim(ClaimTypes.NameIdentifier, userId));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"))
            }
        };
        return controller;
    }

    [Fact]
    public async Task ReserveBook_WithoutUserIdClaim_ReturnsUnauthorized()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call expected.")), userId: null);

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 1 });

        Assert.IsType<UnauthorizedObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReserveBook_AsAdmin_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call expected.")), userId: "admin-id");

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 1 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReserveBook_WhenBookNotFoundInInventory_ReturnsNotFound()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ => JsonResponse.NotFound()));

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        Assert.IsType<NotFoundObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReserveBook_WhenNoCopiesAvailable_ReturnsBadRequest()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            JsonResponse.Ok(new { AvailableCopies = 0 })));

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        Assert.IsType<BadRequestObjectResult>(result.Result);
    }

    [Fact]
    public async Task ReserveBook_WhenCopiesAreAvailable_CreatesPendingReservation()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            JsonResponse.Ok(new { AvailableCopies = 3 })));

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        var created = Assert.IsType<CreatedAtActionResult>(result.Result);
        var response = Assert.IsType<ReservationResponse>(created.Value);
        Assert.Equal(42, response.BookId);
        Assert.Equal(20, response.UserId);
        Assert.Equal("Pending", response.Status);
    }

    [Fact]
    public async Task ReserveBook_WhenInventoryServiceUnreachable_Returns503()
    {
        var controller = CreateController(FakeHttpMessageHandler.ThrowingRequestException());

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(503, statusResult.StatusCode);
    }

    [Fact]
    public async Task ReserveBook_WhenInventoryBaseUrlMissing_Returns500()
    {
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            throw new InvalidOperationException("No HTTP call expected.")), inventoryBaseUrl: null);

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        var statusResult = Assert.IsType<ObjectResult>(result.Result);
        Assert.Equal(500, statusResult.StatusCode);
    }

    [Fact]
    public async Task ReserveBook_WhenCopiesAreAvailable_QueuesReservationCreatedEvent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var controller = CreateController(new FakeHttpMessageHandler(_ =>
            JsonResponse.Ok(new { AvailableCopies = 3 })), context: context);

        var result = await controller.ReserveBook(new ReserveBookRequest { BookId = 42 });

        var response = Assert.IsType<ReservationResponse>(Assert.IsType<CreatedAtActionResult>(result.Result).Value);
        var message = Assert.Single(context.ReservationEvents);
        Assert.Equal("reservation-created", message.EventType);
        Assert.Equal(response.Id, message.ReservationId);
        using var payload = JsonDocument.Parse(message.Payload);
        Assert.Equal(response.Id, payload.RootElement.GetProperty("reservationId").GetInt32());
        Assert.Equal(20, payload.RootElement.GetProperty("userId").GetInt32());
        Assert.Equal(42, payload.RootElement.GetProperty("bookId").GetInt32());
        Assert.Equal(message.Id, payload.RootElement.GetProperty("eventId").GetGuid());
    }

    [Fact]
    public async Task ReserveBook_WhenReservationIsNotCreated_QueuesNoEvent()
    {
        using var context = ControllerTestFactory.CreateDbContext();
        var noCopies = CreateController(new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { AvailableCopies = 0 })), context: context);
        var missingBook = CreateController(new FakeHttpMessageHandler(_ => JsonResponse.NotFound()), context: context);
        var admin = CreateController(new FakeHttpMessageHandler(_ => JsonResponse.Ok(new { AvailableCopies = 3 })), userId: "admin-id", context: context);

        await noCopies.ReserveBook(new ReserveBookRequest { BookId = 42 });
        await missingBook.ReserveBook(new ReserveBookRequest { BookId = 42 });
        await admin.ReserveBook(new ReserveBookRequest { BookId = 42 });

        Assert.Empty(context.Reservations);
        Assert.Empty(context.ReservationEvents);
    }
}

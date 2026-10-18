using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Net.Http.Json;
using LendingService.IntegrationTests.TestSupport;

namespace LendingService.IntegrationTests;

public class ReserveBookTests : IntegrationTestBase
{
    /// <summary>
    /// Reserving first asks the User Service whether the member is still approved (#92), then asks
    /// Inventory about the book. This answers the member check (approved unless told otherwise) and
    /// hands every other call to the test's own Inventory response.
    /// </summary>
    private static Func<HttpRequestMessage, HttpResponseMessage> Downstream(Func<HttpResponseMessage> inventory, bool memberApproved = true) =>
        request => request.RequestUri!.AbsolutePath.Contains("/api/user/profile")
            ? JsonResponse.Ok(new { isValidated = memberApproved })
            : inventory();

    [Fact]
    public async Task ReserveBook_WithoutToken_Returns401()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReserveBook_WhenCopiesAreAvailable_CreatesReservationAndPersistsToDatabase()
    {
        Factory.Responder.Responder = Downstream(() => JsonResponse.Ok(new { availableCopies = 3 }));
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal(42, body.GetProperty("bookId").GetInt32());
        Assert.Equal(20, body.GetProperty("userId").GetInt32());
        Assert.Equal("Pending", body.GetProperty("status").GetString());
        var reservationId = body.GetProperty("id").GetInt32();

        using var db = Factory.CreateDbContext();
        var saved = await db.Reservations.SingleAsync(r => r.Id == reservationId);
        Assert.Equal(42, saved.BookId);
        Assert.Equal(20, saved.UserId);
        Assert.Equal("Pending", saved.Status);
    }

    [Fact]
    public async Task ReserveBook_WhenNoCopiesAvailable_ReturnsBadRequestAndPersistsNothing()
    {
        Factory.Responder.Responder = Downstream(() => JsonResponse.Ok(new { availableCopies = 0 }));
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var db = Factory.CreateDbContext();
        Assert.Empty(db.Reservations);
    }

    [Fact]
    public async Task ReserveBook_WhenBookNotFoundInInventory_ReturnsNotFound()
    {
        Factory.Responder.Responder = Downstream(JsonResponse.NotFound);
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 999 });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ReserveBook_WhenInventoryServiceIsUnreachable_Returns503()
    {
        Factory.Responder.Responder = Downstream(() => throw new HttpRequestException("simulated network failure"));
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task ReserveBook_WhenMemberIsNoLongerApproved_Returns403AndPersistsNothing()
    {
        // A member whose access was revoked still holds a valid token; the live approval check must block them (#92).
        Factory.Responder.Responder = Downstream(() => JsonResponse.Ok(new { availableCopies = 3 }), memberApproved: false);
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        using var db = Factory.CreateDbContext();
        Assert.Empty(db.Reservations);
    }

    [Fact]
    public async Task ReserveBook_WhenMemberAlreadyHasAPendingReservation_Returns409AndKeepsOne()
    {
        // Repeated Reserve clicks must not create duplicate pending reservations (#91).
        Factory.Responder.Responder = Downstream(() => JsonResponse.Ok(new { availableCopies = 3 }));
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var first = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });
        var second = await client.PostAsJsonAsync("/api/reservations", new { bookId = 42 });

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        using var db = Factory.CreateDbContext();
        Assert.Single(db.Reservations);
    }

    [Fact]
    public async Task ReserveBook_WithNonNumericBookIdInBody_ReturnsBadRequestFromModelBinding()
    {
        // Proves [ApiController]'s automatic model-state validation actually runs through the
        // real ASP.NET Core pipeline (a type mismatch fails binding before the action runs).
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsJsonAsync("/api/reservations", new { bookId = "not-a-number" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var db = Factory.CreateDbContext();
        Assert.Empty(db.Reservations);
    }
}

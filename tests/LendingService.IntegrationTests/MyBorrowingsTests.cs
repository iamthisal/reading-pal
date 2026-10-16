using Microsoft.EntityFrameworkCore;
using System.Net;
using LendingService.IntegrationTests.TestSupport;
using LendingService.Models;

namespace LendingService.IntegrationTests;

public class MyBorrowingsTests : IntegrationTestBase
{
    [Fact]
    public async Task Get_WithoutToken_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/my-borrowings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_ReturnsOnlyTheCallingUsersLoansAndReservations()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            db.Reservations.Add(new Reservation { Id = 2, BookId = 11, UserId = 99, Status = "Pending" }); // a different user
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = _ => JsonResponse.Ok(new { title = "Clean Code" });
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.GetAsync("/api/my-borrowings");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        var pending = body.GetProperty("pending").EnumerateArray().ToList();
        var reservation = Assert.Single(pending);
        Assert.Equal(1, reservation.GetProperty("id").GetInt32());
        Assert.Equal("Clean Code", reservation.GetProperty("bookTitle").GetString());
    }

    [Fact]
    public async Task Get_WhenInventoryIsUnavailable_StillReturnsRecordsWithPlaceholderTitle()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = _ => throw new HttpRequestException("simulated network failure");
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.GetAsync("/api/my-borrowings");

        // A catalogue outage must not hide the user's loans or financial records.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        var reservation = Assert.Single(body.GetProperty("pending").EnumerateArray());
        Assert.Contains("title unavailable", reservation.GetProperty("bookTitle").GetString());
    }

    [Fact]
    public async Task Cancel_OwnPendingReservation_CancelsItAndQueuesEvent()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsync("/api/my-borrowings/reservations/1/cancel", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Cancelled", reservation.Status);
    }

    [Fact]
    public async Task Cancel_AnotherUsersReservation_ReturnsNotFound()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 99, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsync("/api/my-borrowings/reservations/1/cancel", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Pending", reservation.Status);
    }

    [Fact]
    public async Task Cancel_ReservationAlreadyBorrowed_ReturnsConflict()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsync("/api/my-borrowings/reservations/1/cancel", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

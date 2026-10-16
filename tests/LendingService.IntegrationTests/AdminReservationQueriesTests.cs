using System.Net;
using LendingService.IntegrationTests.TestSupport;
using LendingService.Models;

namespace LendingService.IntegrationTests;

public class AdminReservationQueriesTests : IntegrationTestBase
{
    /// <summary>Answers the User Service active/pending group lookups and Inventory Service book-title lookups that GetPending/GetBorrowed/GetHistory all make.</summary>
    private static HttpResponseMessage RespondToLookup(HttpRequestMessage request)
    {
        var path = request.RequestUri!.AbsolutePath;
        if (path.Contains("/api/admin/users/active"))
            return JsonResponse.Ok(new[] { new { id = 20, firstName = "Jane", lastName = "Doe" } });
        if (path.Contains("/api/admin/users/pending"))
            return JsonResponse.Ok(Array.Empty<object>());
        if (path.Contains("/api/Books/10"))
            return JsonResponse.Ok(new { title = "Clean Code" });
        throw new InvalidOperationException($"Unexpected lookup request to {path}");
    }

    [Fact]
    public async Task GetPending_AsAdmin_ReturnsPendingReservationsWithUserAndBookDetails()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = RespondToLookup;
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        var reservation = Assert.Single(body.EnumerateArray());
        Assert.Equal("Jane Doe", reservation.GetProperty("userName").GetString());
        Assert.Equal("Clean Code", reservation.GetProperty("bookTitle").GetString());
        Assert.Equal("Pending", reservation.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetPending_WithNoPendingReservations_ReturnsEmptyArrayWithoutCallingDownstreamServices()
    {
        Factory.Responder.Responder = _ => throw new InvalidOperationException("No HTTP call expected when there is nothing pending.");
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Empty(body.EnumerateArray());
    }

    [Fact]
    public async Task GetBorrowed_AsAdmin_FlagsOverdueLoansAndComputesFineAmount()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
            db.BorrowRecords.Add(new BorrowRecord
            {
                Id = 1,
                ReservationId = 1,
                BookId = 10,
                UserId = 20,
                CheckoutDate = DateTime.UtcNow.AddDays(-20),
                DueDate = DateTime.UtcNow.AddDays(-6)
            });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = RespondToLookup;
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/borrowed");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        var record = Assert.Single(body.EnumerateArray());
        Assert.True(record.GetProperty("isOverdue").GetBoolean());
        Assert.True(record.GetProperty("daysOverdue").GetInt32() > 0);
        Assert.True(record.GetProperty("fineAmount").GetDecimal() > 0);
    }

    [Fact]
    public async Task GetHistory_AsAdmin_ReturnsCancelledAndReturnedReservationsButNotActiveOnes()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Cancelled" });
            db.Reservations.Add(new Reservation { Id = 2, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = RespondToLookup;
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/history");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        var entry = Assert.Single(body.EnumerateArray());
        Assert.Equal(1, entry.GetProperty("id").GetInt32());
        Assert.Equal("Cancelled", entry.GetProperty("status").GetString());
    }

    [Fact]
    public async Task GetPending_WhenUserServiceLookupFails_Returns503()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = _ => throw new HttpRequestException("simulated network failure");
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }
}

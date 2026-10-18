using Microsoft.EntityFrameworkCore;
using System.Net;
using LendingService.IntegrationTests.TestSupport;
using LendingService.Models;

namespace LendingService.IntegrationTests;

public class AcceptRejectTests : IntegrationTestBase
{
    [Fact]
    public async Task Accept_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/999/accept", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Accept_AsRegularUser_Returns403()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("5"));

        var response = await client.PostAsync("/api/reservations/1/accept", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Accept_WhenPending_ChecksOutWithInventoryAndPersistsBorrowRecord()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        var checkoutDate = new DateTime(2026, 9, 20, 12, 0, 0, DateTimeKind.Utc);
        Factory.Responder.Responder = request =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Contains("/api/Books/10/checkouts/1", request.RequestUri!.AbsolutePath);
            return JsonResponse.Ok(new { checkoutDateUtc = checkoutDate });
        };
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/1/accept", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("Borrowed", body.GetProperty("status").GetString());

        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Borrowed", reservation.Status);
        var borrowRecord = await verify.BorrowRecords.SingleAsync(b => b.ReservationId == 1);
        Assert.Equal(checkoutDate, borrowRecord.CheckoutDate);
        Assert.Equal(checkoutDate.AddDays(14), borrowRecord.DueDate);
    }

    [Fact]
    public async Task Accept_WhenInventoryCannotAllocateACopy_ReturnsConflictAndKeepsReservationInAcceptingState()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = _ => JsonResponse.Conflict();
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/1/accept", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Accepting", reservation.Status);
        Assert.Empty(verify.BorrowRecords);
    }

    [Fact]
    public async Task Reject_WhenReservationDoesNotExist_ReturnsNotFound()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/999/reject", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Reject_WhenPending_CancelsReservationWithoutCallingInventory()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            await db.SaveChangesAsync();
        }
        Factory.Responder.Responder = _ => throw new InvalidOperationException("Reject must not call any downstream service.");
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/1/reject", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Cancelled", reservation.Status);
    }

    [Fact]
    public async Task Reject_WhenAlreadyAccepted_ReturnsConflict()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed" });
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/reservations/1/reject", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

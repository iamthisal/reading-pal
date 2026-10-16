using Microsoft.EntityFrameworkCore;
using System.Net;
using LendingService.IntegrationTests.TestSupport;
using LendingService.Models;

namespace LendingService.IntegrationTests;

public class ReturnsTests : IntegrationTestBase
{
    private static async Task SeedBorrowedLoanAsync(LendingServiceApiFactory factory, DateTime dueDate)
    {
        using var db = factory.CreateDbContext();
        db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Borrowed", DueDate = dueDate });
        db.BorrowRecords.Add(new BorrowRecord
        {
            Id = 40,
            ReservationId = 1,
            BookId = 10,
            UserId = 20,
            CheckoutDate = dueDate.AddDays(-14),
            DueDate = dueDate
        });
        await db.SaveChangesAsync();
    }

    [Fact]
    public async Task Return_WhenBorrowRecordDoesNotExist_ReturnsNotFound()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/returns/999", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Return_AsRegularUser_Returns403()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("20"));

        var response = await client.PostAsync("/api/returns/40", content: null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Return_WhenOnTime_CompletesLoanWithoutFine()
    {
        var dueDate = DateTime.UtcNow.AddDays(3);
        await SeedBorrowedLoanAsync(Factory, dueDate);
        Factory.Responder.Responder = request =>
        {
            Assert.Contains("/api/Books/10/checkouts/1/return", request.RequestUri!.AbsolutePath);
            return JsonResponse.Ok(new { });
        };
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/returns/40", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal(0, body.GetProperty("daysOverdue").GetInt32());
        Assert.Equal(0m, body.GetProperty("fineAmount").GetDecimal());

        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Returned", reservation.Status);
        Assert.Empty(verify.Fines);
    }

    [Fact]
    public async Task Return_WhenOverdue_PersistsCalculatedFine()
    {
        var dueDate = DateTime.UtcNow.AddDays(-5);
        await SeedBorrowedLoanAsync(Factory, dueDate);
        Factory.Responder.Responder = _ => JsonResponse.Ok(new { });
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/returns/40", content: null);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.True(body.GetProperty("daysOverdue").GetInt32() > 0);
        Assert.True(body.GetProperty("fineAmount").GetDecimal() > 0);

        using var verify = Factory.CreateDbContext();
        var fine = await verify.Fines.SingleAsync(f => f.BorrowRecordId == 40);
        Assert.Equal("Unpaid", fine.Status);
    }

    [Fact]
    public async Task Return_WhenInventoryIsUnavailable_Returns503AndPreservesRetryState()
    {
        var dueDate = DateTime.UtcNow.AddDays(3);
        await SeedBorrowedLoanAsync(Factory, dueDate);
        Factory.Responder.Responder = _ => throw new HttpRequestException("simulated network failure");
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/returns/40", content: null);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);

        using var verify = Factory.CreateDbContext();
        var reservation = await verify.Reservations.SingleAsync(r => r.Id == 1);
        Assert.Equal("Returning", reservation.Status);
        var loan = await verify.BorrowRecords.SingleAsync(b => b.Id == 40);
        Assert.NotNull(loan.ReturnRequestedAtUtc);
        Assert.Null(loan.ReturnDate);
    }

    [Fact]
    public async Task Return_WhenLoanIsNotBorrowed_ReturnsConflict()
    {
        using (var db = Factory.CreateDbContext())
        {
            db.Reservations.Add(new Reservation { Id = 1, BookId = 10, UserId = 20, Status = "Pending" });
            db.BorrowRecords.Add(new BorrowRecord { Id = 40, ReservationId = 1, BookId = 10, UserId = 20,
                CheckoutDate = DateTime.UtcNow, DueDate = DateTime.UtcNow.AddDays(14) });
            await db.SaveChangesAsync();
        }
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.PostAsync("/api/returns/40", content: null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }
}

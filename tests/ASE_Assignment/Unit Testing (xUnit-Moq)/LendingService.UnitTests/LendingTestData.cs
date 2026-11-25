using System.Security.Claims;
using LendingService.Data;
using LendingService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LendingService.UnitTests;

internal static class LendingTestData
{
    internal static readonly DateTime Now = new(2026, 10, 18, 6, 0, 0, DateTimeKind.Utc);
    internal static LendingDbContext CreateDb() => new(new DbContextOptionsBuilder<LendingDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    internal static Reservation Reservation(string status = "Pending") => new()
    {
        Id = 1, BookId = 10, UserId = 5, Status = status, ReservationDate = Now
    };
    internal static BorrowRecord Loan(int daysUntilDue = 2) => new()
    {
        Id = 1, ReservationId = 1, BookId = 10, UserId = 5, CheckoutDate = Now.AddDays(-12), DueDate = Now.AddDays(daysUntilDue)
    };
    internal static ReservationEventOutbox Message() => new()
    {
        Id = Guid.NewGuid(), ReservationId = 1, CreatedAtUtc = Now,
        EventType = "reservation-created", Payload = "{\"eventType\":\"reservation-created\"}"
    };
    internal static void SignIn(ControllerBase controller, string? subject)
    {
        var claims = subject == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, subject) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }
}

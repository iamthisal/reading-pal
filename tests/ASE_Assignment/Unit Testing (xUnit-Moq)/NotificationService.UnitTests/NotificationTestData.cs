using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NotificationService.Data;
using NotificationService.Kafka;

namespace NotificationService.UnitTests;

internal static class NotificationTestData
{
    internal static NotificationDbContext CreateDb() => new(new DbContextOptionsBuilder<NotificationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    internal static LendingEvent Accepted() => new()
    {
        EventId = Guid.NewGuid(), EventType = LendingEventTypes.ReservationAccepted,
        ReservationId = 1, UserId = 5, BookId = 10, DueDate = new DateTime(2026, 10, 20, 6, 0, 0, DateTimeKind.Utc)
    };
    internal static CatalogEvent NewBook() => new()
    {
        EventId = Guid.NewGuid(), EventType = CatalogEventTypes.BookCreated, BookId = 10,
        Book = new CatalogBook { Title = "Dune", Author = "Frank Herbert" }
    };
    internal static void SignIn(ControllerBase controller, string subject)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, subject) }, "Test"))
            }
        };
    }
}

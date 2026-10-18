using System.Security.Claims;
using InventoryService.Controllers;
using InventoryService.Data;
using InventoryService.DTOs;
using InventoryService.Kafka;
using InventoryService.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace InventoryService.UnitTests;

internal static class InventoryTestData
{
    internal static InventoryDbContext CreateDb() => new(new DbContextOptionsBuilder<InventoryDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static Book Book() => new()
    {
        Id = 1, Title = "Dune", Author = "Frank Herbert", ISBN = "ISBN-1", Genre = "Fiction",
        TotalCopies = 5, AvailableCopies = 3, Description = "Original description"
    };

    internal static BooksController Books(InventoryDbContext db) => new(db,
        new Mock<IBookEventPublisher>().Object, new Mock<ILogger<BooksController>>().Object);

    internal static UpdateBookRequest Update(int copies) => new()
    {
        Title = " Dune ", Author = " Frank Herbert ", ISBN = "ISBN-1", Genre = " Fiction ", TotalCopies = copies
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

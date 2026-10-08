using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NotificationService.Data;
using NotificationService.Services;

namespace NotificationService.Tests.TestSupport;

public static class TestFactory
{
    public static NotificationDbContext CreateDbContext() =>
        new(new DbContextOptionsBuilder<NotificationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    public static LendingEventHandler CreateHandler(NotificationDbContext db, string? title = "Clean Code") =>
        new(db, new FixedTitleLookup(title), NullLogger<LendingEventHandler>.Instance);

    public static void AttachUser(ControllerBase controller, string userId, string role = "User", DateTime? activeSince = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, userId),
            new(ClaimTypes.Role, role)
        };
        if (activeSince is { } since)
            claims.Add(new Claim("active_since", new DateTimeOffset(since).ToUnixTimeSeconds().ToString()));
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }

    public static CatalogEventHandler CreateCatalogHandler(NotificationDbContext db) =>
        new(db, NullLogger<CatalogEventHandler>.Instance);

    /// <summary>Serializes an event the way Lending does (camelCase web defaults).</summary>
    public static string EventJson(object evt) => JsonSerializer.Serialize(evt, new JsonSerializerOptions(JsonSerializerDefaults.Web));
}

public sealed class FixedTitleLookup(string? title) : IBookTitleLookup
{
    public int Calls { get; private set; }

    public Task<string?> GetTitleAsync(int bookId, CancellationToken cancellationToken)
    {
        Calls++;
        return Task.FromResult(title);
    }
}

public sealed class FakeCustomerDirectory(IReadOnlyDictionary<int, string> names) : ICustomerDirectory
{
    public string? LastAuthorizationHeader { get; private set; }

    public Task<IReadOnlyDictionary<int, string>> GetNamesAsync(string adminAuthorizationHeader, CancellationToken cancellationToken)
    {
        LastAuthorizationHeader = adminAuthorizationHeader;
        return Task.FromResult(names);
    }
}

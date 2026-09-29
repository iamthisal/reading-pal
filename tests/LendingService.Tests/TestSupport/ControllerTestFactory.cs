using LendingService.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LendingService.Tests.TestSupport;

public static class ControllerTestFactory
{
    /// <summary>
    /// Gives the controller an HttpContext so code that reads <c>Request.Headers</c>
    /// (e.g. forwarding the Authorization header downstream) doesn't null-reference.
    /// </summary>
    public static void AttachHttpContext(ControllerBase controller)
    {
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    public static LendingDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<LendingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new LendingDbContext(options);
    }

    public static IConfiguration CreateConfiguration(
        string? userServiceBaseUrl = "http://user-service.test",
        string? inventoryServiceBaseUrl = "http://inventory-service.test")
    {
        var values = new Dictionary<string, string?>
        {
            ["UserService:BaseUrl"] = userServiceBaseUrl,
            ["InventoryService:BaseUrl"] = inventoryServiceBaseUrl
        };
        return new ConfigurationBuilder().AddInMemoryCollection(values).Build();
    }
}

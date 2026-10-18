using System.Net;
using LendingService.IntegrationTests.TestSupport;

namespace LendingService.IntegrationTests;

public class HealthCheckTests : IntegrationTestBase
{
    [Fact]
    public async Task Health_ReturnsOkWithHealthyStatus()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await BodyAsync(response);
        Assert.Equal("healthy", body.GetProperty("status").GetString());
    }
}

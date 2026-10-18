using System.Net;
using LendingService.IntegrationTests.TestSupport;

namespace LendingService.IntegrationTests;

/// <summary>
/// Exercises the real JWT-bearer authentication and role-based authorization middleware end to
/// end (unit tests call controllers directly and never touch this pipeline at all).
/// </summary>
public class AuthenticationTests : IntegrationTestBase
{
    [Fact]
    public async Task AdminOnlyEndpoint_WithoutToken_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithUserRoleToken_Returns403()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateUserToken("5"));

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task AdminOnlyEndpoint_WithAdminRoleToken_IsAuthorized()
    {
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UserOnlyEndpoint_WithoutToken_Returns401()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/my-borrowings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UserOnlyEndpoint_WithAdminRoleToken_Returns403()
    {
        // MyBorrowingsController requires the "User" role specifically — an Admin token must not
        // be treated as "any authenticated principal".
        using var client = CreateClient(JwtTestTokenFactory.CreateAdminToken());

        var response = await client.GetAsync("/api/my-borrowings");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_WithExpiredToken_Returns401()
    {
        var expiredToken = JwtTestTokenFactory.CreateToken("5", "User", lifetime: TimeSpan.FromMinutes(-5));
        using var client = CreateClient(expiredToken);

        var response = await client.GetAsync("/api/my-borrowings");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Endpoint_WithTokenSignedByAnUntrustedKey_Returns401()
    {
        var forgedToken = JwtTestTokenFactory.CreateTokenSignedWithWrongKey("5", "Admin");
        using var client = CreateClient(forgedToken);

        var response = await client.GetAsync("/api/reservations/pending");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

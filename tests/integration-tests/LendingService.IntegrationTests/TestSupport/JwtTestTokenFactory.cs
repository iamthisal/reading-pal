using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace LendingService.IntegrationTests.TestSupport;

/// <summary>
/// Mints JWTs signed with the same key/issuer/audience <see cref="LendingServiceApiFactory"/>
/// configures the app to trust, so tests exercise the real JWT-bearer authentication pipeline
/// (signature + issuer + audience + lifetime validation) instead of bypassing it.
/// </summary>
public static class JwtTestTokenFactory
{
    public static string CreateToken(string userId, string role, TimeSpan? lifetime = null)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(LendingServiceApiFactory.JwtKey));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: LendingServiceApiFactory.JwtIssuer,
            audience: LendingServiceApiFactory.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(30)),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static string CreateAdminToken(string userId = "1") => CreateToken(userId, "Admin");

    public static string CreateUserToken(string userId) => CreateToken(userId, "User");

    /// <summary>Signs with a key the app does not trust, to prove signature validation is actually enforced.</summary>
    public static string CreateTokenSignedWithWrongKey(string userId, string role)
    {
        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("A-Completely-Different-Untrusted-Signing-Key-256-Bits!!"));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: LendingServiceApiFactory.JwtIssuer,
            audience: LendingServiceApiFactory.JwtAudience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(30),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}

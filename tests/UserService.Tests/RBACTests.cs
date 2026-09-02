using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace UserService.Tests
{
    public class RBACTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly HttpClient _client;

        // JWT settings used by the User Service
        private const string JwtKey = "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough";
        private const string JwtIssuer = "ReadingPal.UserService";
        private const string JwtAudience = "ReadingPal.Frontend";

        public RBACTests(WebApplicationFactory<Program> factory)
        {
            _client = factory.CreateClient();
        }

        // Admin should be able to access the admin endpoint
        [Fact]
        public async Task TC_RBAC_001_Admin_CanAccessAdminEndpoint_Returns200()
        {
            var token = GenerateJwtToken("admin-id", "admin@library.com", "Admin", DateTime.UtcNow.AddHours(2));
            SetBearerToken(token);

            var response = await _client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // Normal user should not be allowed to access the admin endpoint
        [Fact]
        public async Task TC_RBAC_002_NormalUser_CannotAccessAdminEndpoint_Returns403()
        {
            var token = GenerateJwtToken("Dahamya", "dahamyakulandi21@gmail.com", "User", DateTime.UtcNow.AddHours(2));
            SetBearerToken(token);

            var response = await _client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        }

        // Request without a JWT should be rejected
        [Fact]
        public async Task TC_RBAC_003_NoJwt_CannotAccessProtectedEndpoint_Returns401()
        {
            _client.DefaultRequestHeaders.Authorization = null;

            var response = await _client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Invalid JWT should be rejected
        [Fact]
        public async Task TC_RBAC_004_InvalidJwt_CannotAccessProtectedEndpoint_Returns401()
        {
            SetBearerToken("invalid.jwt.token");

            var response = await _client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Expired JWT should be rejected
        [Fact]
        public async Task TC_RBAC_005_ExpiredJwt_CannotAccessProtectedEndpoint_Returns401()
        {
            var token = GenerateExpiredJwtToken("Dahamya", "dahamyakulandi21@gmail.com", "User");
            SetBearerToken(token);

            var response = await _client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // Add the token to the request header
        private void SetBearerToken(string token)
        {
            _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }

        // Create a normal JWT with the given role
        private static string GenerateJwtToken(string userId, string email, string role, DateTime expires)
        {
            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("IsValidated", "True")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = expires,
                Issuer = JwtIssuer,
                Audience = JwtAudience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }

        // Create a JWT that has already expired
        private static string GenerateExpiredJwtToken(string userId, string email, string role)
        {
            var now = DateTime.UtcNow;

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim(JwtRegisteredClaimNames.Email, email),
                new Claim(ClaimTypes.Role, role),
                new Claim("IsValidated", "True")
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),

                // Set these times in the past so the token is already expired
                NotBefore = now.AddMinutes(-20),
                IssuedAt = now.AddMinutes(-20),
                Expires = now.AddMinutes(-10),

                Issuer = JwtIssuer,
                Audience = JwtAudience,
                SigningCredentials = credentials
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);

            return tokenHandler.WriteToken(token);
        }
    }
}
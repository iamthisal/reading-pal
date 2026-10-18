using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using Xunit;

namespace UserService.Tests
{
    // The active_since claim lets the Notification Service show catalogue announcements only to
    // customers who were registered and active when the announcement was made.
    public class ActiveSinceTests
    {
        private static UserDbContext CreateDbContext() =>
            new(new DbContextOptionsBuilder<UserDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

        private static IConfiguration CreateConfiguration() => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                { "AdminCredentials:Email", "admin@library.com" },
                { "AdminCredentials:Password", "adminpassword" },
                { "Jwt:Key", "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough" },
                { "Jwt:Issuer", "ReadingPal.UserService" },
                { "Jwt:Audience", "ReadingPal.Frontend" }
            })
            .Build();

        private static User NewUser(bool validated, DateTime createdAt, DateTime? approvedAt = null) => new()
        {
            Id = 1,
            Email = "reader@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Hello1234"),
            FirstName = "Read",
            LastName = "Er",
            Role = "User",
            IsValidated = validated,
            CreatedAt = createdAt,
            ApprovedAtUtc = approvedAt
        };

        private static string? ActiveSinceClaim(UserDbContext context)
        {
            var result = new AuthController(context, CreateConfiguration())
                .Login(new LoginRequest { Email = "reader@example.com", Password = "Hello1234" });
            var token = Assert.IsType<AuthResponse>(Assert.IsType<OkObjectResult>(result).Value).Token;
            return new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.FirstOrDefault(c => c.Type == "active_since")?.Value;
        }

        private static long Unix(DateTime utc) => new DateTimeOffset(utc).ToUnixTimeSeconds();

        [Fact]
        public void AcceptUser_RecordsApprovalTime()
        {
            using var context = CreateDbContext();
            context.Users.Add(NewUser(validated: false, createdAt: DateTime.UtcNow.AddDays(-1)));
            context.SaveChanges();

            var before = DateTime.UtcNow;
            Assert.IsType<OkObjectResult>(new AdminController(context).AcceptUser(1));

            var approvedAt = context.Users.Single().ApprovedAtUtc;
            Assert.NotNull(approvedAt);
            Assert.InRange(approvedAt!.Value, before, DateTime.UtcNow);
        }

        [Fact]
        public void RevokeUser_ClearsApprovalTime()
        {
            using var context = CreateDbContext();
            context.Users.Add(NewUser(validated: true, createdAt: DateTime.UtcNow.AddDays(-5), approvedAt: DateTime.UtcNow.AddDays(-4)));
            context.SaveChanges();

            Assert.IsType<OkObjectResult>(new AdminController(context).RevokeUser(1));

            Assert.Null(context.Users.Single().ApprovedAtUtc);
        }

        [Fact]
        public void Login_ApprovedUser_TokenCarriesApprovalTime()
        {
            using var context = CreateDbContext();
            var approvedAt = new DateTime(2026, 10, 1, 9, 0, 0, DateTimeKind.Utc);
            context.Users.Add(NewUser(validated: true, createdAt: approvedAt.AddDays(-2), approvedAt: approvedAt));
            context.SaveChanges();

            Assert.Equal(Unix(approvedAt).ToString(), ActiveSinceClaim(context));
        }

        [Fact]
        public void Login_UserApprovedBeforeTrackingExisted_FallsBackToRegistrationTime()
        {
            using var context = CreateDbContext();
            var createdAt = new DateTime(2026, 8, 29, 7, 0, 0, DateTimeKind.Utc);
            context.Users.Add(NewUser(validated: true, createdAt: createdAt));
            context.SaveChanges();

            Assert.Equal(Unix(createdAt).ToString(), ActiveSinceClaim(context));
        }

        [Fact]
        public void Login_UnapprovedUser_TokenHasNoActiveSince()
        {
            using var context = CreateDbContext();
            context.Users.Add(NewUser(validated: false, createdAt: DateTime.UtcNow));
            context.SaveChanges();

            Assert.Null(ActiveSinceClaim(context));
        }
    }
}

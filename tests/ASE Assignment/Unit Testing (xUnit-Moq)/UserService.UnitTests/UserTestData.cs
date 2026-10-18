using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using UserService.Data;
using UserService.Models;

namespace UserService.UnitTests;

internal static class UserTestData
{
    internal static UserDbContext CreateDb() => new(new DbContextOptionsBuilder<UserDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    internal static User Member(int id = 1) => new()
    {
        Id = id, Email = $"reader{id}@example.com", FirstName = "Asha", LastName = "Perera",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!"), Role = "User"
    };

    internal static Mock<IConfiguration> Configuration()
    {
        var jwt = new Mock<IConfigurationSection>();
        jwt.Setup(section => section["Key"]).Returns("assignment-test-signing-key-at-least-32-characters");
        jwt.Setup(section => section["Issuer"]).Returns("ASE-tests");
        jwt.Setup(section => section["Audience"]).Returns("ASE-tests");
        var config = new Mock<IConfiguration>();
        config.Setup(settings => settings.GetSection("Jwt")).Returns(jwt.Object);
        return config;
    }

    internal static void SignIn(ControllerBase controller, string? subject)
    {
        var claims = subject == null ? Array.Empty<Claim>() : new[] { new Claim(ClaimTypes.NameIdentifier, subject) };
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
        };
    }
}

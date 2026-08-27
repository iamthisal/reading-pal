using System;
using System.Collections.Generic;
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
    public class AuthControllerTests
    {
        private readonly IConfiguration _configuration;

        public AuthControllerTests()
        {
            var configValues = new Dictionary<string, string?>
            {
                { "AdminCredentials:Email", "admin@readingpal.local" },
                { "AdminCredentials:Password", "AdminPassword123!" },
                { "Jwt:Key", "SuperSecretKeyThatIsAtLeast32BytesLongForTesting123456" },
                { "Jwt:Issuer", "ReadingPal" },
                { "Jwt:Audience", "ReadingPal" }
            };

            _configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(configValues)
                .Build();
        }

        private UserDbContext CreateInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new UserDbContext(options);
        }

        [Fact]
        public void Login_WithValidAdminCredentials_ReturnsOkWithAdminRole()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var controller = new AuthController(context, _configuration);

            var request = new LoginRequest
            {
                Email = "admin@readingpal.local",
                Password = "AdminPassword123!"
            };

            // Act
            var result = controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal("Admin", response.Role);
            Assert.True(response.IsValidated);
            Assert.False(string.IsNullOrEmpty(response.Token));
        }

        [Fact]
        public void Login_WithValidUserCredentials_ReturnsOkWithUserRole()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("UserPassword123!");
            var user = new User
            {
                Id = 1,
                Email = "reader@readingpal.local",
                PasswordHash = passwordHash,
                Role = "User",
                FirstName = "Jane",
                LastName = "Doe",
                IsValidated = true
            };
            context.Users.Add(user);
            context.SaveChanges();

            var controller = new AuthController(context, _configuration);
            var request = new LoginRequest
            {
                Email = "reader@readingpal.local",
                Password = "UserPassword123!"
            };

            // Act
            var result = controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);
            Assert.Equal("User", response.Role);
            Assert.True(response.IsValidated);
            Assert.False(string.IsNullOrEmpty(response.Token));
        }

        [Fact]
        public void Login_WithInvalidPassword_ReturnsUnauthorized()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var passwordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword123!");
            var user = new User
            {
                Id = 2,
                Email = "reader2@readingpal.local",
                PasswordHash = passwordHash,
                Role = "User"
            };
            context.Users.Add(user);
            context.SaveChanges();

            var controller = new AuthController(context, _configuration);
            var request = new LoginRequest
            {
                Email = "reader2@readingpal.local",
                Password = "WrongPassword"
            };

            // Act
            var result = controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        [Fact]
        public void Login_WithNonExistentUser_ReturnsUnauthorized()
        {
            // Arrange
            using var context = CreateInMemoryDbContext();
            var controller = new AuthController(context, _configuration);

            var request = new LoginRequest
            {
                Email = "unknown@readingpal.local",
                Password = "SomePassword"
            };

            // Act
            var result = controller.Login(request);

            // Assert
            Assert.IsType<UnauthorizedObjectResult>(result);
        }
    }
}

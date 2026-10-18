using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using System.Net.Http.Json;
using Xunit;

namespace UserService.Tests
{
    public class LoginTest
    {
        private UserDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new UserDbContext(options);
        }

        private IConfiguration CreateConfiguration()
        {
            var settings = new Dictionary<string, string?>
            {
                { "AdminCredentials:Email", "admin@library.com" },
                { "AdminCredentials:Password", "adminpassword" },
                { "Jwt:Key", "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough" },
                { "Jwt:Issuer", "ReadingPal.UserService" },
                { "Jwt:Audience", "ReadingPal.Frontend" }
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        // TC-LOGIN-001 - Valid login
        [Fact]
        public void Login_WithValidCredentials_ReturnsJwtToken()
        {
            using var context = CreateDbContext();
            var configuration = CreateConfiguration();

            var user = new User
            {
                Id = 1,
                Email = "dahamyakulandi12@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Hello1234"),
                FirstName = "Dahamya",
                LastName = "Kulandi",
                Role = "User",
                IsValidated = true
            };

            context.Users.Add(user);
            context.SaveChanges();

            var controller = new AuthController(context, configuration);

            var request = new LoginRequest
            {
                Email = "dahamyakulandi12@gmail.com",
                Password = "Hello1234"
            };

            var result = controller.Login(request);

            var okResult = Assert.IsType<OkObjectResult>(result);
            var response = Assert.IsType<AuthResponse>(okResult.Value);

            Assert.NotEmpty(response.Token);
            Assert.Equal("User", response.Role);
            Assert.True(response.IsValidated);
        }

        // TC-LOGIN-002 - Wrong password
        [Fact]
        public void Login_WithWrongPassword_ReturnsUnauthorized()
        {
            using var context = CreateDbContext();
            var configuration = CreateConfiguration();

            var user = new User
            {
                Id = 1,
                Email = "dahamyakulandi12@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Hello1234"),
                FirstName = "Dahamya",
                LastName = "Kulandi",
                Role = "User",
                IsValidated = true
            };

            context.Users.Add(user);
            context.SaveChanges();

            var controller = new AuthController(context, configuration);

            var request = new LoginRequest
            {
                Email = "dahamyakulandi12@gmail.com",
                Password = "*123"
            };

            var result = controller.Login(request);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // TC-LOGIN-003 - Non-existent email
        [Fact]
        public void Login_WithNonExistentEmail_ReturnsUnauthorized()
        {
            using var context = CreateDbContext();
            var configuration = CreateConfiguration();

            var controller = new AuthController(context, configuration);

            var request = new LoginRequest
            {
                Email = "hello@gmail.com",
                Password = "Password123"
            };

            var result = controller.Login(request);

            Assert.IsType<UnauthorizedObjectResult>(result);
        }

        // TC-LOGIN-004 - Empty credentials
        [Fact]
        public void Login_WithEmptyCredentials_FailsValidation()
        {
            var request = new LoginRequest
            {
                Email = "",
                Password = ""
            };

            var validationContext =
                new System.ComponentModel.DataAnnotations.ValidationContext(request);

            var validationResults =
                new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            var isValid =
                System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                    request,
                    validationContext,
                    validationResults,
                    true);

            Assert.False(isValid);
            Assert.NotEmpty(validationResults);
        }

        // TC-LOGIN-005 - Invalid JWT
        [Fact]
        public async Task AdminEndpoint_WithInvalidJwt_ReturnsUnauthorized()
        {
            await using var application = new WebApplicationFactory<Program>();

            using var client = application.CreateClient();

            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(
                    "Bearer",
                    "invalid.jwt.token"
                );

            var response = await client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // TC-LOGIN-006 - Missing JWT
        [Fact]
        public async Task AdminEndpoint_WithoutJwt_ReturnsUnauthorized()
        {
            await using var application = new WebApplicationFactory<Program>();

            using var client = application.CreateClient();

            var response = await client.GetAsync("/api/admin/message");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }

        // TC-LOGIN-007 - Valid JWT
        [Fact]
        public async Task AdminEndpoint_WithValidJwt_ReturnsOk()
        {
            await using var application = new WebApplicationFactory<Program>();
            using var client = application.CreateClient();
            // First login using the valid admin credentials
            var loginRequest = new {
                Email = "admin@library.com",
                Password = "adminpassword"
            };

            var loginResponse = await client.PostAsJsonAsync("/api/auth/login", loginRequest);
            loginResponse.EnsureSuccessStatusCode();
            
            var loginData = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
            
            Assert.NotNull(loginData);
            Assert.NotEmpty(loginData.Token);
            
            // Use the JWT returned from login
            client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer",
                loginData.Token);

            // Access protected admin endpoint
            var response = await client.GetAsync("/api/admin/message");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }
}
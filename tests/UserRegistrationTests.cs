using System;
using System.Collections.Generic;
using System.Linq;
using System.ComponentModel.DataAnnotations;
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
    public class UserRegistrationTests
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
                ["Jwt:Key"] =
                    "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough",
                ["Jwt:Issuer"] = "ReadingPal.UserService",
                ["Jwt:Audience"] = "ReadingPal.Frontend"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(settings)
                .Build();
        }

        private AuthController CreateController(UserDbContext context)
        {
            return new AuthController(
                context,
                CreateConfiguration()
            );
        }

        private bool IsValid(RegisterRequest request)
        {
            var validationContext = new ValidationContext(request);
            var validationResults = new List<ValidationResult>();

            return Validator.TryValidateObject(
                request,
                validationContext,
                validationResults,
                true
            );
        }


        // TC-REG-001
        [Fact]
        public void Register_ValidData_CreatesUser()
        {
            // Arrange
            using var context = CreateDbContext();
            var controller = CreateController(context);

            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "Password123!",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var result = controller.Register(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(result);

            var user = context.Users
                .SingleOrDefault(u => u.Email == request.Email);

            Assert.NotNull(user);
            Assert.Equal("User", user.Role);
            Assert.Equal("Test", user.FirstName);
            Assert.Equal("User", user.LastName);
            Assert.False(user.IsValidated);
        }


        // TC-REG-002
        [Fact]
        public void Register_DuplicateEmail_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateDbContext();

            context.Users.Add(new User
            {
                Email = "existing@example.com",
                PasswordHash =
                    BCrypt.Net.BCrypt.HashPassword("Password123!"),
                FirstName = "Existing",
                LastName = "User",
                Role = "User"
            });

            context.SaveChanges();

            var controller = CreateController(context);

            var request = new RegisterRequest
            {
                Email = "existing@example.com",
                Password = "Password456!",
                FirstName = "New",
                LastName = "User"
            };

            // Act
            var result = controller.Register(request);

            // Assert
            var badRequest =
                Assert.IsType<BadRequestObjectResult>(result);

            Assert.Equal(400, badRequest.StatusCode);

            Assert.Single(
                context.Users.Where(
                    u => u.Email == "existing@example.com"
                )
            );
        }


        // TC-REG-003
        [Fact]
        public void Register_InvalidEmail_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "dahamya@",
                Password = "Password123!",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var valid = IsValid(request);

            // Assert
            Assert.False(valid);
        }


        // TC-REG-004
        [Fact]
        public void Register_EmptyName_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "Password123!",
                FirstName = "",
                LastName = ""
            };

            // Act
            var valid = IsValid(request);

            // Assert
            Assert.False(valid);
        }


        // TC-REG-005
        [Fact]
        public void Register_EmptyEmail_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "",
                Password = "Password123!",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var valid = IsValid(request);

            // Assert
            Assert.False(valid);
        }


        // TC-REG-006
        [Fact]
        public void Register_EmptyPassword_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var valid = IsValid(request);

            // Assert
            Assert.False(valid);
        }


        // TC-REG-007
        [Fact]
        public void Register_WeakPassword_FailsValidation()
        {
            // Arrange
            var request = new RegisterRequest
            {
                Email = "test@example.com",
                Password = "123",
                FirstName = "Test",
                LastName = "User"
            };

            // Act
            var valid = IsValid(request);

            // Assert
            Assert.False(valid);
        }


        // TC-REG-008
        [Fact]
        public void Register_PasswordIsHashed_NotPlainText()
        {
            // Arrange
            using var context = CreateDbContext();
            var controller = CreateController(context);

            var originalPassword = "Password123!";

            var request = new RegisterRequest
            {
                Email = "secure@example.com",
                Password = originalPassword,
                FirstName = "Secure",
                LastName = "User"
            };

            // Act
            controller.Register(request);

            // Assert
            var user = context.Users
                .SingleOrDefault(
                    u => u.Email == "secure@example.com"
                );

            Assert.NotNull(user);

            // Password must NOT be stored as plaintext
            Assert.NotEqual(
                originalPassword,
                user.PasswordHash
            );

            // But the original password must verify
            Assert.True(
                BCrypt.Net.BCrypt.Verify(
                    originalPassword,
                    user.PasswordHash
                )
            );
        }
    }
}
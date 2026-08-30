using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;
using UserService.Controllers;
using UserService.Data;
using UserService.DTOs;
using UserService.Models;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace UserService.Tests
{
    public class ProfileUpdateTests
    {
        private UserDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<UserDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;

            return new UserDbContext(options);
        }

        private IConfiguration GetConfiguration()
        {
            var configurationData = new Dictionary<string, string?>
            {
                ["Jwt:Key"] = "ThisIsAVerySecretKeyThatShouldBeStoredSecurelyAndLongEnough",
                ["Jwt:Issuer"] = "ReadingPal.UserService",
                ["Jwt:Audience"] = "ReadingPal.Frontend",
                ["AdminCredentials:Email"] = "admin@library.com",
                ["AdminCredentials:Password"] = "adminpassword"
            };

            return new ConfigurationBuilder()
                .AddInMemoryCollection(configurationData)
                .Build();
        }

        private User CreateTestUser(
            UserDbContext context,
            string email = "dahamyakulandi12@gmail.com")
        {
            var user = new User
            {
                Id = 1,
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Pwd123*"),
                FirstName = "Dahamya",
                LastName = "Kulandi",
                Role = "User",
                IsValidated = true
            };

            context.Users.Add(user);
            context.SaveChanges();

            return user;
        }

        private UserController CreateController(
            UserDbContext context,
            int userId)
        {
            var controller = new UserController(context);

            var claims = new List<Claim>
            {
                new Claim("sub", userId.ToString())
            };

            var identity = new ClaimsIdentity(
                claims,
                "TestAuthentication");

            var principal = new ClaimsPrincipal(identity);

            controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = principal
                }
            };

            return controller;
        }

        // TC-PROFILE-001
        [Fact]
        public void GetProfile_WithValidJwt_ReturnsOwnProfile()
        {
            // Arrange
            using var context = GetDbContext();
            var user = CreateTestUser(context);

            var controller = CreateController(context, user.Id);

            // Act
            var result = controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

            var profile =
                Assert.IsType<UserProfileResponse>(okResult.Value);

            Assert.Equal("Dahamya", profile.FirstName);
            Assert.Equal("Kulandi", profile.LastName);
            Assert.Equal("dahamyakulandi12@gmail.com", profile.Email);
        }

        // TC-PROFILE-002
        [Fact]
        public void UpdateProfile_WithValidDetails_UpdatesProfile()
        {
            // Arrange
            using var context = GetDbContext();
            var user = CreateTestUser(context);

            var controller = CreateController(context, user.Id);

            var request = new UpdateProfileRequest
            {
                FirstName = "Kulandi",
                LastName = "Wickramasinghe",
                Email = "dahamya12@gmail.com",
                Password = null
            };

            // Act
            var result = controller.UpdateProfile(request);

            // Assert
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

            var updatedUser = context.Users.Single(u => u.Id == user.Id);

            Assert.Equal("Kulandi", updatedUser.FirstName);
            Assert.Equal("Wickramasinghe", updatedUser.LastName);
            Assert.Equal("dahamya12@gmail.com", updatedUser.Email);
        }

        // TC-PROFILE-003
        [Fact]
        public void UpdateProfile_WithInvalidEmail_ReturnsValidationError()
        {
            // Arrange
            using var context = GetDbContext();
            var user = CreateTestUser(context);

            var controller = CreateController(context, user.Id);

            var request = new UpdateProfileRequest
            {
                FirstName = "Dahamya",
                LastName = "Kulandi",
                Email = "daham12",
                Password = "Pwd123*"
            };

            var validationContext = new System.ComponentModel.DataAnnotations.ValidationContext(request);
            var validationResults = new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Act
            var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            request,
            validationContext,
            validationResults,
            true);

            // Assert
            Assert.False(isValid);



            // Act
            //var result = controller.UpdateProfile(request);

            // Assert
            //Assert.IsType<BadRequestObjectResult>(result);
            
            // Make sure the invalid email was not saved
            //var updatedUser = context.Users.Single(u => u.Id == user.Id);
            //Assert.Equal("dahamyakulandi12@gmail.com", updatedUser.Email);
        }

        // TC-PROFILE-004
        [Fact]
        public void UpdateProfile_WithMissingRequiredFields_FailsValidation()
        {
            // Arrange
            var request = new UpdateProfileRequest
            {
                FirstName = "",
                LastName = "",
                Email = "",
                Password = null
            };

            var validationContext =
                new System.ComponentModel.DataAnnotations.ValidationContext(request);

            var validationResults =
                new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Act
            var isValid =
                System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                    request,
                    validationContext,
                    validationResults,
                    true);

            // Assert
            Assert.False(isValid);
        }

        // TC-PROFILE-005
        [Fact]
        public void UpdatePassword_WithShortPassword_FailsValidation()
        {
            // Arrange
            var request = new UpdateProfileRequest
            {
                FirstName = "Dahamya",
                LastName = "Kulandi",
                Email = "dahamya12@gmail.com",
                Password = "123"
            };

            var validationContext =
                new System.ComponentModel.DataAnnotations.ValidationContext(request);

            var validationResults =
                new List<System.ComponentModel.DataAnnotations.ValidationResult>();

            // Act
            var isValid =
                System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
                    request,
                    validationContext,
                    validationResults,
                    true);

            // Assert
            Assert.False(isValid);
        }

        // TC-PROFILE-006
        [Fact]
        public void UpdateProfile_WithoutPassword_KeepsExistingPassword()
        {
            // Arrange
            using var context = GetDbContext();
            var user = CreateTestUser(context);

            var originalPasswordHash = user.PasswordHash;

            var controller = CreateController(context, user.Id);

            var request = new UpdateProfileRequest
            {
                FirstName = "Kulandi",
                LastName = "Wickramasinghe",
                Email = "dahamya12@gmail.com",
                Password = null
            };

            // Act
            controller.UpdateProfile(request);

            // Assert
            var updatedUser = context.Users.Single(u => u.Id == user.Id);

            Assert.Equal(originalPasswordHash, updatedUser.PasswordHash);
            Assert.Equal("Kulandi", updatedUser.FirstName);
            Assert.Equal("Wickramasinghe", updatedUser.LastName);
        }

        // TC-PROFILE-007
        [Fact]
        public void UpdateProfile_WithValidPassword_ChangesPassword()
        {
            // Arrange
            using var context = GetDbContext();
            var user = CreateTestUser(context);

            var oldPasswordHash = user.PasswordHash;

            var controller = CreateController(context, user.Id);

            var request = new UpdateProfileRequest
            {
                FirstName = "Kulandi",
                LastName = "Wickramasinghe",
                Email = "dahamya12@gmail.com",
                Password = "NwPwd123*"
            };

            // Act
            var result = controller.UpdateProfile(request);

            // Assert
            Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

            var updatedUser = context.Users.Single(u => u.Id == user.Id);

            Assert.NotEqual(oldPasswordHash, updatedUser.PasswordHash);

            Assert.True(
                BCrypt.Net.BCrypt.Verify(
                    "NwPwd123*",
                    updatedUser.PasswordHash));
        }

        // TC-PROFILE-008
        [Fact]
        public void UpdateProfile_WithExistingEmail_ReturnsBadRequest()
        {
            // Arrange
            using var context = GetDbContext();

            var userA = CreateTestUser(context, "dahamya12@gmail.com");

            var userB = new User
            {
                Id = 2,
                Email = "lithu12@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                FirstName = "Lithuli",
                LastName = "Limansa",
                Role = "User",
                IsValidated = true
            };

            context.Users.Add(userB);
            context.SaveChanges();

            var controller = CreateController(context, userA.Id);

            var request = new UpdateProfileRequest
            {
                FirstName = "Kulandi",
                LastName = "Wickramasinghe",
                Email = "lithu12@gmail.com",
                Password = null
            };

            // Act
            var result = controller.UpdateProfile(request);

            // Assert
            var badRequest =
                Assert.IsType<Microsoft.AspNetCore.Mvc.BadRequestObjectResult>(result);

            Assert.NotNull(badRequest.Value);

            var unchangedUser =
                context.Users.Single(u => u.Id == userA.Id);

            Assert.Equal("dahamya12@gmail.com", unchangedUser.Email);
        }

        // TC-PROFILE-009
        [Fact]
        public void GetProfile_WithoutJwt_ReturnsUnauthorized()
        {
            // Arrange
            using var context = GetDbContext();
            CreateTestUser(context);

            var controller = new UserController(context);

            controller.ControllerContext =
                new Microsoft.AspNetCore.Mvc.ControllerContext
                {
                    HttpContext = new DefaultHttpContext
                    {
                        User = new ClaimsPrincipal(
                            new ClaimsIdentity())
                    }
                };

            // Act
            var result = controller.GetProfile();

            // Assert
            Assert.IsType<Microsoft.AspNetCore.Mvc.UnauthorizedObjectResult>(result);
        }

        // TC-PROFILE-010
        [Fact]
        public void GetProfile_WithDifferentUserId_ReturnsOnlyThatAuthenticatedUsersProfile()
        {
            // Arrange
            using var context = GetDbContext();

            var userA = CreateTestUser(context, "dahamya12@gmail.com");

            var userB = new User
            {
                Id = 2,
                Email = "lithu12@gmail.com",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("password123"),
                FirstName = "Lithuli",
                LastName = "Limansa",
                Role = "User",
                IsValidated = true
            };

            context.Users.Add(userB);
            context.SaveChanges();

            // Authenticate as User A
            var controller = CreateController(context, userA.Id);

            // Act
            var result = controller.GetProfile();

            // Assert
            var okResult =
                Assert.IsType<Microsoft.AspNetCore.Mvc.OkObjectResult>(result);

            var profile =
                Assert.IsType<UserProfileResponse>(okResult.Value);

            Assert.Equal("dahamya12@gmail.com", profile.Email);
            Assert.NotEqual("lithu12@gmail.com", profile.Email);
        }
    }
}
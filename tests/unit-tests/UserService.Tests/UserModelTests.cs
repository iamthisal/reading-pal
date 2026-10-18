using System;
using UserService.Models;
using Xunit;

namespace UserService.Tests
{
    public class UserModelTests
    {
        [Fact]
        public void User_DefaultValues_AreProperlyInitialized()
        {
            // Act
            var user = new User();

            // Assert
            Assert.Equal(0, user.Id);
            Assert.Equal(string.Empty, user.Email);
            Assert.Equal(string.Empty, user.PasswordHash);
            Assert.Equal("User", user.Role);
            Assert.Equal(string.Empty, user.FirstName);
            Assert.Equal(string.Empty, user.LastName);
            Assert.False(user.IsValidated);
            Assert.True(user.CreatedAt <= DateTime.UtcNow);
        }

        [Fact]
        public void User_CanSetProperties_Correctly()
        {
            // Arrange & Act
            var now = DateTime.UtcNow;
            var user = new User
            {
                Id = 42,
                Email = "test@example.com",
                PasswordHash = "hashed_pw",
                Role = "Admin",
                FirstName = "First",
                LastName = "Last",
                IsValidated = true,
                CreatedAt = now
            };

            // Assert
            Assert.Equal(42, user.Id);
            Assert.Equal("test@example.com", user.Email);
            Assert.Equal("hashed_pw", user.PasswordHash);
            Assert.Equal("Admin", user.Role);
            Assert.Equal("First", user.FirstName);
            Assert.Equal("Last", user.LastName);
            Assert.True(user.IsValidated);
            Assert.Equal(now, user.CreatedAt);
        }
    }
}

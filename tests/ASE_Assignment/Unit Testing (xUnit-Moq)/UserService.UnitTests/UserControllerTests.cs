using Microsoft.AspNetCore.Mvc;
using UserService.Controllers;
using UserService.DTOs;

namespace UserService.UnitTests;

public class UserControllerTests
{
    [Fact]
    // Proves profile updates keep the password when no replacement is supplied.
    public void UpdateProfile_NullPassword_KeepsPasswordHash()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        var originalHash = user.PasswordHash;
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new UserController(db);
        UserTestData.SignIn(controller, "1");
        // Act
        var result = controller.UpdateProfile(new UpdateProfileRequest { Email = user.Email, FirstName = "Nimal", LastName = "Silva", Password = null });
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.Equal("Nimal", db.Users.Single().FirstName);
        Assert.Equal(originalHash, db.Users.Single().PasswordHash);
    }

    [Fact]
    // Proves a supplied replacement password is hashed and saved.
    public void UpdateProfile_NewPassword_ReplacesPasswordHash()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new UserController(db);
        UserTestData.SignIn(controller, "1");
        // Act
        var result = controller.UpdateProfile(new UpdateProfileRequest { Email = user.Email, FirstName = "Asha", LastName = "Perera", Password = "Replacement123!" });
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.True(BCrypt.Net.BCrypt.Verify("Replacement123!", db.Users.Single().PasswordHash));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    // Proves missing, empty and malformed token identities cannot read a profile.
    public void GetProfile_InvalidIdentity_ReturnsUnauthorized(string? subject)
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var controller = new UserController(db);
        UserTestData.SignIn(controller, subject);
        // Act
        var result = controller.GetProfile();
        // Assert
        Assert.True(result is UnauthorizedObjectResult);
    }

    [Fact]
    // Proves a valid identity with no matching account is reported as missing.
    public void GetProfile_MissingUser_ReturnsNotFound()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var controller = new UserController(db);
        UserTestData.SignIn(controller, "99");
        // Act
        var result = controller.GetProfile();
        // Assert
        Assert.True(result is NotFoundObjectResult);
    }

    [Fact]
    // Proves changing to another user's email leaves the original profile intact.
    public void UpdateProfile_DuplicateEmail_DoesNotChangeProfile()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        var other = UserTestData.Member(2);
        db.Users.AddRange(user, other);
        db.SaveChanges();
        var controller = new UserController(db);
        UserTestData.SignIn(controller, "1");
        // Act
        var result = controller.UpdateProfile(new UpdateProfileRequest { Email = other.Email, FirstName = "Changed" });
        // Assert
        Assert.True(result is BadRequestObjectResult);
        Assert.Equal("reader1@example.com", user.Email);
        Assert.Equal("Asha", user.FirstName);
    }
}

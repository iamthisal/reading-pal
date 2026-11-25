using Microsoft.AspNetCore.Mvc;
using UserService.Controllers;
using UserService.DTOs;

namespace UserService.UnitTests;

public class AuthControllerTests
{
    [Fact]
    // Proves registration saves an unapproved user with a hashed password.
    public void Register_NewEmail_SavesUnapprovedUser()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var config = UserTestData.Configuration();
        var controller = new AuthController(db, config.Object);
        var request = new RegisterRequest { Email = "new@example.com", Password = "Secret123!", FirstName = "Asha", LastName = "Perera" };
        // Act
        var result = controller.Register(request);
        // Assert
        var saved = db.Users.Single();
        Assert.True(result is OkObjectResult);
        Assert.Equal(request.Email, saved.Email);
        Assert.Equal("User", saved.Role);
        Assert.False(saved.IsValidated);
        Assert.True(BCrypt.Net.BCrypt.Verify(request.Password, saved.PasswordHash));
        config.Verify(settings => settings.GetSection("Jwt"), Moq.Times.Never);
    }

    [Fact]
    // Proves a duplicate email cannot create a second account.
    public void Register_DuplicateEmail_DoesNotAddUser()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new AuthController(db, UserTestData.Configuration().Object);
        // Act
        var result = controller.Register(new RegisterRequest { Email = user.Email, Password = "Secret123!" });
        // Assert
        Assert.True(result is BadRequestObjectResult);
        Assert.Equal(1, db.Users.Count());
    }

    [Fact]
    // Proves correct credentials produce a user token and read JWT settings once.
    public void Login_CorrectPassword_ReturnsUserToken()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        user.IsValidated = true;
        db.Users.Add(user);
        db.SaveChanges();
        var config = UserTestData.Configuration();
        var controller = new AuthController(db, config.Object);
        // Act
        var result = controller.Login(new LoginRequest { Email = user.Email, Password = "Correct123!" });
        // Assert
        var response = (AuthResponse)((OkObjectResult)result).Value!;
        Assert.Equal("User", response.Role);
        Assert.True(response.IsValidated);
        Assert.False(string.IsNullOrWhiteSpace(response.Token));
        config.Verify(settings => settings.GetSection("Jwt"), Moq.Times.Once);
    }

    [Theory]
    [InlineData("")]
    [InlineData("Wrong123!")]
    // Proves empty and incorrect passwords cannot produce a token.
    public void Login_InvalidPassword_ReturnsUnauthorized(string password)
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        db.Users.Add(user);
        db.SaveChanges();
        var config = UserTestData.Configuration();
        var controller = new AuthController(db, config.Object);
        // Act
        var result = controller.Login(new LoginRequest { Email = user.Email, Password = password });
        // Assert
        Assert.True(result is UnauthorizedObjectResult);
        config.Verify(settings => settings.GetSection("Jwt"), Moq.Times.Never);
    }

    [Fact]
    // Proves an unknown email is rejected before token generation.
    public void Login_UnknownEmail_ReturnsUnauthorized()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var config = UserTestData.Configuration();
        var controller = new AuthController(db, config.Object);
        // Act
        var result = controller.Login(new LoginRequest { Email = "missing@example.com", Password = "Secret123!" });
        // Assert
        Assert.True(result is UnauthorizedObjectResult);
        config.Verify(settings => settings.GetSection("Jwt"), Moq.Times.Never);
    }
}

using Microsoft.AspNetCore.Mvc;
using UserService.Controllers;

namespace UserService.UnitTests;

public class AdminControllerTests
{
    [Fact]
    // Proves accepting a pending member records approval and enables access.
    public void AcceptUser_PendingUser_ApprovesUser()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new AdminController(db);
        // Act
        var result = controller.AcceptUser(user.Id);
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.True(user.IsValidated);
        Assert.True(user.ApprovedAtUtc.HasValue);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(99)]
    // Proves invalid and unknown IDs cannot approve a member.
    public void AcceptUser_MissingId_ReturnsNotFound(int id)
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var controller = new AdminController(db);
        // Act
        var result = controller.AcceptUser(id);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        Assert.Equal(0, db.Users.Count());
    }

    [Fact]
    // Proves rejecting a pending member deletes the account.
    public void RejectUser_PendingUser_DeletesUser()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        db.Users.Add(UserTestData.Member());
        db.SaveChanges();
        var controller = new AdminController(db);
        // Act
        var result = controller.RejectUser(1);
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.Equal(0, db.Users.Count());
    }

    [Fact]
    // Proves revoking a member clears both approval and its timestamp.
    public void RevokeUser_ActiveUser_ClearsApproval()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        user.IsValidated = true;
        user.ApprovedAtUtc = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new AdminController(db);
        // Act
        var result = controller.RevokeUser(1);
        // Assert
        Assert.True(result is OkObjectResult);
        Assert.False(user.IsValidated);
        Assert.Equal<DateTime?>(null, user.ApprovedAtUtc);
    }

    [Fact]
    // Proves administrator access cannot be revoked by the member-revocation action.
    public void RevokeUser_AdminAccount_KeepsApproval()
    {
        // Arrange
        using var db = UserTestData.CreateDb();
        var user = UserTestData.Member();
        user.Role = "Admin";
        user.IsValidated = true;
        db.Users.Add(user);
        db.SaveChanges();
        var controller = new AdminController(db);
        // Act
        var result = controller.RevokeUser(1);
        // Assert
        Assert.True(result is NotFoundObjectResult);
        Assert.True(user.IsValidated);
    }
}

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationService.Services;

namespace NotificationService.UnitTests;

public class LookupFallbackTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    // Proves missing or invalid inventory configuration returns no title without HTTP.
    public async Task GetTitleAsync_InvalidBaseUrl_ReturnsNull(string? baseUrl)
    {
        // Arrange
        var clients = new Mock<IHttpClientFactory>();
        var config = new Mock<IConfiguration>();
        config.Setup(settings => settings["InventoryService:BaseUrl"]).Returns(baseUrl);
        var lookup = new InventoryBookTitleLookup(clients.Object, config.Object, new Mock<ILogger<InventoryBookTitleLookup>>().Object);
        // Act
        var title = await lookup.GetTitleAsync(10, CancellationToken.None);
        // Assert
        Assert.Equal<string?>(null, title);
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-url")]
    // Proves missing or invalid user-service configuration returns no names without HTTP.
    public async Task GetNamesAsync_InvalidBaseUrl_ReturnsEmptyNames(string? baseUrl)
    {
        // Arrange
        var clients = new Mock<IHttpClientFactory>();
        var config = new Mock<IConfiguration>();
        config.Setup(settings => settings["UserService:BaseUrl"]).Returns(baseUrl);
        var directory = new UserServiceCustomerDirectory(clients.Object, config.Object, new Mock<ILogger<UserServiceCustomerDirectory>>().Object);
        // Act
        var names = await directory.GetNamesAsync("Bearer test", CancellationToken.None);
        // Assert
        Assert.Equal(0, names.Count);
        clients.Verify(factory => factory.CreateClient(string.Empty), Times.Never);
    }
}

using InventoryService.Controllers;
using InventoryService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.UnitTests;

public class BookSearchControllerTests
{
    [Theory]
    [InlineData(-1, 12)]
    [InlineData(0, 12)]
    [InlineData(1, 1)]
    [InlineData(48, 48)]
    [InlineData(49, 48)]
    // Proves page sizes use the default below one and stop at the maximum of 48.
    public async Task Search_PageSizeBoundary_UsesAllowedSize(int requested, int expected)
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var controller = new BookSearchController(db);
        // Act
        var result = await controller.Search(pageSize: requested);
        // Assert
        var response = (BookSearchResponse)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(expected, response.PageSize);
        Assert.Equal(1, response.Page);
    }

    [Fact]
    // Proves search excludes unavailable books when availability is requested.
    public async Task Search_AvailableOnly_ExcludesUnavailableBook()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var book = InventoryTestData.Book();
        book.AvailableCopies = 0;
        db.Books.Add(book);
        await db.SaveChangesAsync();
        var controller = new BookSearchController(db);
        // Act
        var result = await controller.Search(q: "Dune", availableOnly: true);
        // Assert
        var response = (BookSearchResponse)((OkObjectResult)result.Result!).Value!;
        Assert.Equal(0, response.Total);
        Assert.Equal(0, response.Items.Count);
    }
}

using InventoryService.DTOs;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.UnitTests;

public class BooksControllerTests
{
    [Fact]
    // Proves creating a book trims text and puts all copies on the shelf.
    public async Task Create_ValidBook_SavesTrimmedBook()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var controller = InventoryTestData.Books(db);
        var request = new CreateBookRequest { Title = " Dune ", Author = " Frank Herbert ", ISBN = " ISBN-1 ", Genre = " Fiction ", TotalCopies = 3 };
        // Act
        var result = await controller.Create(request);
        // Assert
        var saved = db.Books.Single();
        Assert.True(result.Result is CreatedAtActionResult);
        Assert.Equal("Dune", saved.Title);
        Assert.Equal("ISBN-1", saved.ISBN);
        Assert.Equal(3, saved.AvailableCopies);
    }

    [Fact]
    // Proves ISBN duplicates are rejected even with different casing and spaces.
    public async Task Create_DuplicateIsbn_ReturnsConflict()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.Create(new CreateBookRequest { ISBN = " isbn-1 " });
        // Assert
        Assert.True(result.Result is ConflictObjectResult);
        Assert.Equal(1, db.Books.Count());
    }

    [Theory]
    [InlineData(1, false, 3)]
    [InlineData(2, true, 0)]
    [InlineData(3, true, 1)]
    // Proves total copies cannot fall below the two copies already borrowed.
    public async Task Update_CopyBoundary_PreservesBorrowedCopies(int total, bool accepted, int available)
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.Update(1, InventoryTestData.Update(total));
        // Assert
        Assert.Equal(accepted, result.Result is OkObjectResult);
        Assert.Equal(available, db.Books.Single().AvailableCopies);
        Assert.Equal(accepted ? total : 5, db.Books.Single().TotalCopies);
    }

    [Fact]
    // Proves an omitted description keeps the existing description.
    public async Task Update_NullDescription_KeepsExistingDescription()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.Update(1, InventoryTestData.Update(5));
        // Assert
        Assert.True(result.Result is OkObjectResult);
        Assert.Equal("Original description", db.Books.Single().Description);
    }

    [Fact]
    // Proves a book with borrowed copies cannot be deleted.
    public async Task Delete_BorrowedCopies_ReturnsConflict()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.Delete(1);
        // Assert
        Assert.True(result is ConflictObjectResult);
        Assert.Equal(1, db.Books.Count());
    }

    [Fact]
    // Proves deleting an unknown book reports a missing resource.
    public async Task Delete_MissingBook_ReturnsNotFound()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.Delete(99);
        // Assert
        Assert.True(result is NotFoundObjectResult);
    }

    [Fact]
    // Proves marking unavailable removes shelf copies while keeping borrowed copies.
    public async Task MarkUnavailable_BorrowedCopies_KeepsBorrowedTotal()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = InventoryTestData.Books(db);
        // Act
        var result = await controller.MarkUnavailable(1);
        // Assert
        Assert.True(result.Result is OkObjectResult);
        Assert.Equal(0, db.Books.Single().AvailableCopies);
        Assert.Equal(2, db.Books.Single().TotalCopies);
    }

    [Fact]
    // Proves invalid model state prevents a book from being saved.
    public async Task Create_InvalidModel_DoesNotSaveBook()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var controller = InventoryTestData.Books(db);
        controller.ModelState.AddModelError("Title", "Title is required");
        // Act
        var result = await controller.Create(new CreateBookRequest());
        // Assert
        Assert.True(result.Result is BadRequestObjectResult);
        Assert.Equal(0, db.Books.Count());
    }
}

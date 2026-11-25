using InventoryService.Controllers;
using InventoryService.DTOs;
using InventoryService.Models;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.UnitTests;

public class CatalogueRulesTests
{
    [Fact]
    // Proves a genre assigned to a book cannot be removed.
    public async Task Delete_GenreInUse_ReturnsConflict()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Genres.Add(new Genre { Id = 1, Name = "Fiction" });
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = new GenresController(db);
        // Act
        var result = await controller.Delete(1);
        // Assert
        Assert.True(result is ConflictObjectResult);
        Assert.Equal(1, db.Genres.Count());
    }

    [Fact]
    // Proves renaming an author also changes the author on their books.
    public async Task Update_AuthorRenamed_UpdatesMatchingBooks()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Authors.Add(new Author { Id = 1, Name = "Frank Herbert" });
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = new AuthorsController(db);
        // Act
        var result = await controller.Update(1, new UpdateAuthorRequest { Name = " F. Herbert " });
        // Assert
        Assert.True(result.Result is OkObjectResult);
        Assert.Equal("F. Herbert", db.Authors.Single().Name);
        Assert.Equal("F. Herbert", db.Books.Single().Author);
    }

    [Fact]
    // Proves adding a favourite twice stores only one favourite for that reader.
    public async Task Add_RepeatedFavourite_StoresOneRow()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.Books.Add(InventoryTestData.Book());
        await db.SaveChangesAsync();
        var controller = new FavouritesController(db);
        InventoryTestData.SignIn(controller, "1");
        await controller.Add(1);
        // Act
        var result = await controller.Add(1);
        // Assert
        Assert.True(result is NoContentResult);
        Assert.Equal(1, db.FavouriteBooks.Count());
        Assert.Equal("1", db.FavouriteBooks.Single().UserId);
    }

    [Fact]
    // Proves a missing identity cannot add favourites.
    public async Task Add_MissingIdentity_ReturnsUnauthorized()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        var controller = new FavouritesController(db);
        InventoryTestData.SignIn(controller, null);
        // Act
        var result = await controller.Add(1);
        // Assert
        Assert.True(result is UnauthorizedResult);
        Assert.Equal(0, db.FavouriteBooks.Count());
    }

    [Fact]
    // Proves a curated list cannot contain the same book twice.
    public async Task SetItems_DuplicateBookIds_ReturnsBadRequest()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.CuratedLists.Add(new CuratedList { Id = 1, Key = "top", Name = "Top", ItemType = CuratedListItemType.Books });
        await db.SaveChangesAsync();
        var controller = new ListsController(db);
        // Act
        var result = await controller.SetItems("top", new SetCuratedListItemsRequest { ItemIds = new List<int> { 1, 1 } });
        // Assert
        Assert.True(result.Result is BadRequestObjectResult);
        Assert.Equal(0, db.CuratedListItems.Count());
    }

    [Fact]
    // Proves unknown book IDs cannot be saved as curated-list items.
    public async Task SetItems_UnknownBookId_ReturnsBadRequest()
    {
        // Arrange
        using var db = InventoryTestData.CreateDb();
        db.CuratedLists.Add(new CuratedList { Id = 1, Key = "top", Name = "Top", ItemType = CuratedListItemType.Books });
        await db.SaveChangesAsync();
        var controller = new ListsController(db);
        // Act
        var result = await controller.SetItems("top", new SetCuratedListItemsRequest { ItemIds = new List<int> { 99 } });
        // Assert
        Assert.True(result.Result is BadRequestObjectResult);
        Assert.Equal(0, db.CuratedListItems.Count());
    }
}

using InventoryService.Controllers;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Tests.Controllers;

public class BooksControllerCheckoutTests : IDisposable
{
    private readonly SqliteInventoryDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private static Book NewBook(int id, int availableCopies) => new()
    {
        Id = id,
        Title = "Clean Code",
        Author = "Robert C. Martin",
        ISBN = $"ISBN-{id}",
        Genre = "Software",
        TotalCopies = Math.Max(availableCopies, 1),
        AvailableCopies = availableCopies
    };

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, 1)]
    public async Task Checkout_WithNonPositiveIds_ReturnsBadRequest(int bookId, int reservationId)
    {
        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(bookId, reservationId, CancellationToken.None);

        Assert.IsType<BadRequestObjectResult>(result);
    }

    [Fact]
    public async Task Checkout_WhenBookDoesNotExist_ReturnsNotFound()
    {
        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(id: 999, reservationId: 1, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task Checkout_WhenNoCopiesAvailable_ReturnsConflict()
    {
        using (var setup = _factory.CreateContext())
        {
            setup.Books.Add(NewBook(id: 1, availableCopies: 0));
            await setup.SaveChangesAsync();
        }

        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(id: 1, reservationId: 1, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }

    [Fact]
    public async Task Checkout_WhenACopyIsAvailable_DecrementsAvailableCopiesAndRecordsCheckout()
    {
        using (var setup = _factory.CreateContext())
        {
            setup.Books.Add(NewBook(id: 1, availableCopies: 3));
            await setup.SaveChangesAsync();
        }

        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(id: 1, reservationId: 42, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        using var verify = _factory.CreateContext();
        var book = await verify.Books.FindAsync(1);
        Assert.Equal(2, book!.AvailableCopies);

        var checkout = await verify.BookCheckouts.FindAsync(42);
        Assert.NotNull(checkout);
        Assert.Equal(1, checkout!.BookId);
    }

    [Fact]
    public async Task Checkout_WhenReservationAlreadyCheckedOutSameBook_ReturnsOkWithoutDoubleDeducting()
    {
        using (var setup = _factory.CreateContext())
        {
            setup.Books.Add(NewBook(id: 1, availableCopies: 3));
            await setup.SaveChangesAsync();
        }

        using (var first = _factory.CreateContext())
        {
            var firstController = new BooksController(first);
            await firstController.Checkout(id: 1, reservationId: 42, CancellationToken.None);
        }

        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(id: 1, reservationId: 42, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);

        using var verify = _factory.CreateContext();
        var book = await verify.Books.FindAsync(1);
        Assert.Equal(2, book!.AvailableCopies);
    }

    [Fact]
    public async Task Checkout_WhenReservationAlreadyCheckedOutDifferentBook_ReturnsConflict()
    {
        using (var setup = _factory.CreateContext())
        {
            setup.Books.Add(NewBook(id: 1, availableCopies: 3));
            setup.Books.Add(NewBook(id: 2, availableCopies: 3));
            await setup.SaveChangesAsync();
        }

        using (var first = _factory.CreateContext())
        {
            var firstController = new BooksController(first);
            await firstController.Checkout(id: 1, reservationId: 42, CancellationToken.None);
        }

        using var context = _factory.CreateContext();
        var controller = new BooksController(context);

        var result = await controller.Checkout(id: 2, reservationId: 42, CancellationToken.None);

        Assert.IsType<ConflictObjectResult>(result);
    }
}

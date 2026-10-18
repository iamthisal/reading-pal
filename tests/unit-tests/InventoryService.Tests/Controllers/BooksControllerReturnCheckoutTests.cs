using InventoryService.Controllers;
using InventoryService.Models;
using InventoryService.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Tests.Controllers;

public sealed class BooksControllerReturnCheckoutTests : IDisposable
{
    private readonly SqliteInventoryDbContextFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private async Task SeedCheckout(int bookId = 1, int reservationId = 42, int available = 1, int total = 2)
    {
        using var context = _factory.CreateContext();
        context.Books.Add(new Book
        {
            Id = bookId,
            Title = "Clean Code",
            Author = "Robert C. Martin",
            ISBN = $"RETURN-{bookId}",
            Genre = "Software",
            TotalCopies = total,
            AvailableCopies = available
        });
        context.BookCheckouts.Add(new BookCheckout
        {
            Id = reservationId,
            BookId = bookId,
            CheckoutDateUtc = DateTime.UtcNow.AddDays(-2)
        });
        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task ReturnCheckout_WithMatchingCheckout_RestoresOneCopyAndMarksReturned()
    {
        await SeedCheckout();
        using var context = _factory.CreateContext();

        var result = await new BooksController(context).ReturnCheckout(1, 42, CancellationToken.None);

        Assert.IsType<OkObjectResult>(result);
        using var verify = _factory.CreateContext();
        Assert.Equal(2, (await verify.Books.FindAsync(1))!.AvailableCopies);
        Assert.NotNull((await verify.BookCheckouts.FindAsync(42))!.ReturnDateUtc);
    }

    [Fact]
    public async Task ReturnCheckout_WhenRetried_DoesNotRestoreAnotherCopy()
    {
        await SeedCheckout(available: 0, total: 2);
        using (var first = _factory.CreateContext())
            Assert.IsType<OkObjectResult>(await new BooksController(first).ReturnCheckout(1, 42, CancellationToken.None));

        using (var retry = _factory.CreateContext())
            Assert.IsType<OkObjectResult>(await new BooksController(retry).ReturnCheckout(1, 42, CancellationToken.None));

        using var verify = _factory.CreateContext();
        Assert.Equal(1, (await verify.Books.FindAsync(1))!.AvailableCopies);
    }

    [Theory]
    [InlineData(1, 999)]
    [InlineData(999, 42)]
    public async Task ReturnCheckout_WithoutMatchingCheckout_ReturnsNotFound(int bookId, int reservationId)
    {
        await SeedCheckout();
        using var context = _factory.CreateContext();

        var result = await new BooksController(context).ReturnCheckout(bookId, reservationId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task ReturnCheckout_WhenInventoryIsAlreadyFull_RollsBackReturnMarker()
    {
        await SeedCheckout(available: 2, total: 2);
        using (var context = _factory.CreateContext())
            Assert.IsType<ConflictObjectResult>(await new BooksController(context).ReturnCheckout(1, 42, CancellationToken.None));

        using var verify = _factory.CreateContext();
        Assert.Equal(2, (await verify.Books.FindAsync(1))!.AvailableCopies);
        Assert.Null((await verify.BookCheckouts.FindAsync(42))!.ReturnDateUtc);
    }
}

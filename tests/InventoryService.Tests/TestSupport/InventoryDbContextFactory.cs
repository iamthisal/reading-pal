using InventoryService.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace InventoryService.Tests.TestSupport;

/// <summary>
/// BooksController.Checkout uses ExecuteUpdateAsync and an explicit transaction, which the
/// EF Core InMemory provider doesn't support. SQLite's in-memory mode is a real relational
/// engine, so it exercises the same code path as MySQL in production.
/// </summary>
public sealed class SqliteInventoryDbContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteInventoryDbContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public InventoryDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<InventoryDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new InventoryDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}

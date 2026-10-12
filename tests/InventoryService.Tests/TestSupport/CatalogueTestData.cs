using InventoryService.Models;

namespace InventoryService.Tests.TestSupport;

public static class CatalogueTestData
{
    public static Book Book(string title, string author = "Author", string genre = "Fiction", int available = 1, DateTime? createdAt = null)
    {
        return new Book
        {
            Title = title,
            Author = author,
            ISBN = $"isbn-{Guid.NewGuid():N}"[..30],
            Genre = genre,
            TotalCopies = Math.Max(available, 1),
            AvailableCopies = available,
            CreatedAt = createdAt ?? DateTime.UtcNow,
            UpdatedAt = createdAt ?? DateTime.UtcNow
        };
    }
}

using System.Net.Http.Json;
using System.Text.Json;

namespace NotificationService.Services;

public interface IBookTitleLookup
{
    /// <summary>Returns the book's title, or null when Inventory cannot provide it.</summary>
    Task<string?> GetTitleAsync(int bookId, CancellationToken cancellationToken);
}

public sealed class InventoryBookTitleLookup(IHttpClientFactory clients, IConfiguration configuration,
    ILogger<InventoryBookTitleLookup> logger) : IBookTitleLookup
{
    public async Task<string?> GetTitleAsync(int bookId, CancellationToken cancellationToken)
    {
        if (!Uri.TryCreate(configuration["InventoryService:BaseUrl"], UriKind.Absolute, out var inventoryUrl))
            return null;
        try
        {
            using var client = clients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            using var response = await client.GetAsync(new Uri(inventoryUrl, $"/api/Books/{bookId}"), cancellationToken);
            if (!response.IsSuccessStatusCode) return null;
            var book = await response.Content.ReadFromJsonAsync<BookTitle>(cancellationToken);
            return string.IsNullOrWhiteSpace(book?.Title) ? null : book.Title.Trim();
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // A catalogue outage must not block notifications; the message falls back to the book ID.
            logger.LogWarning(ex, "Could not look up the title of book {BookId}.", bookId);
            return null;
        }
    }

    private sealed class BookTitle { public string? Title { get; set; } }
}

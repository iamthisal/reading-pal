using System.Net.Http.Json;
using System.Text.Json;

namespace NotificationService.Services;

public interface ICustomerDirectory
{
    /// <summary>
    /// Returns customer names by user ID, using the calling admin's token. Missing or unavailable
    /// names are simply absent; callers fall back to "Customer #id".
    /// </summary>
    Task<IReadOnlyDictionary<int, string>> GetNamesAsync(string adminAuthorizationHeader, CancellationToken cancellationToken);
}

/// <summary>
/// Reads names from the User Service's admin user lists, the same lookup Lending's pending-reservation
/// list uses. The User Service only answers admins, so the admin's own token is forwarded.
/// </summary>
public sealed class UserServiceCustomerDirectory(IHttpClientFactory clients, IConfiguration configuration,
    ILogger<UserServiceCustomerDirectory> logger) : ICustomerDirectory
{
    public async Task<IReadOnlyDictionary<int, string>> GetNamesAsync(string adminAuthorizationHeader, CancellationToken cancellationToken)
    {
        var names = new Dictionary<int, string>();
        if (!Uri.TryCreate(configuration["UserService:BaseUrl"], UriKind.Absolute, out var userServiceUrl))
            return names;
        try
        {
            using var client = clients.CreateClient();
            client.Timeout = TimeSpan.FromSeconds(5);
            foreach (var group in new[] { "active", "pending" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(userServiceUrl, $"/api/admin/users/{group}"));
                request.Headers.TryAddWithoutValidation("Authorization", adminAuthorizationHeader);
                using var response = await client.SendAsync(request, cancellationToken);
                if (!response.IsSuccessStatusCode) continue;
                var users = await response.Content.ReadFromJsonAsync<List<UserName>>(cancellationToken) ?? new();
                foreach (var user in users)
                {
                    var name = $"{user.FirstName} {user.LastName}".Trim();
                    if (name.Length > 0) names[user.Id] = name;
                }
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException ||
            (ex is OperationCanceledException && !cancellationToken.IsCancellationRequested))
        {
            // Notifications stay readable when the User Service is down; names fall back to the user ID.
            logger.LogWarning(ex, "Could not load customer names from the User Service.");
        }
        return names;
    }

    private sealed class UserName
    {
        public int Id { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
    }
}

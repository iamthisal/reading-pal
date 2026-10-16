using System.Net.Http.Headers;
using System.Text.Json;

namespace LendingService.IntegrationTests.TestSupport;

/// <summary>
/// Gives each test its own <see cref="LendingServiceApiFactory"/> (own in-memory database, own fake
/// HTTP responder) — xUnit creates a fresh instance of the test class per test method, so the
/// factory created here in the constructor and disposed in <see cref="Dispose"/> never leaks
/// state between tests.
/// </summary>
public abstract class IntegrationTestBase : IDisposable
{
    protected LendingServiceApiFactory Factory { get; } = new();

    protected HttpClient CreateClient(string? bearerToken = null)
    {
        var client = Factory.CreateClient();
        if (bearerToken != null)
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        return client;
    }

    protected static async Task<JsonElement> BodyAsync(HttpResponseMessage response)
    {
        var stream = await response.Content.ReadAsStreamAsync();
        using var document = await JsonDocument.ParseAsync(stream);
        return document.RootElement.Clone();
    }

    public void Dispose() => Factory.Dispose();
}

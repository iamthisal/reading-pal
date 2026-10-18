using System.Net;
using System.Net.Http.Json;

namespace LendingService.IntegrationTests.TestSupport;

/// <summary>
/// Stands in for User Service / Inventory Service. Each test sets <see cref="Responder"/> to a
/// function that inspects the outgoing request (method + path) and returns the response the
/// downstream service would have given, so the controller's real HTTP-calling code path runs
/// end-to-end without any real network access.
/// </summary>
public sealed class RespondingHttpMessageHandler : HttpMessageHandler
{
    public Func<HttpRequestMessage, HttpResponseMessage> Responder { get; set; } =
        request => throw new InvalidOperationException(
            $"No fake response configured for {request.Method} {request.RequestUri}. Set Responder before making the call.");

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        Task.FromResult(Responder(request));
}

public sealed class FakeHttpClientFactory(RespondingHttpMessageHandler handler) : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
}

public static class JsonResponse
{
    public static HttpResponseMessage Ok(object payload) => new(HttpStatusCode.OK)
    {
        Content = JsonContent.Create(payload)
    };

    public static HttpResponseMessage NotFound() => new(HttpStatusCode.NotFound);

    public static HttpResponseMessage Conflict() => new(HttpStatusCode.Conflict);
}

using System.Net;
using System.Net.Http.Json;

namespace LendingService.Tests.TestSupport;

/// <summary>
/// Routes outgoing HTTP requests to a caller-supplied responder so controller tests can
/// simulate User/Inventory Service responses without a real network call.
/// </summary>
public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

    public FakeHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        _responder = responder;
    }

    public static FakeHttpMessageHandler ThrowingRequestException() =>
        new(_ => throw new HttpRequestException("Simulated network failure."));

    /// <summary>
    /// The same handler, except that calls to the User Service's profile endpoint (the approval check made
    /// before a reservation) are answered here, so each test's own responder only has to play Inventory.
    /// </summary>
    public FakeHttpMessageHandler AnsweringProfileWith(Func<HttpResponseMessage> profileResponse) =>
        new(request => request.RequestUri?.AbsolutePath == "/api/user/profile" ? profileResponse() : _responder(request));

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        return Task.FromResult(_responder(request));
    }
}

public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly HttpMessageHandler _handler;

    public FakeHttpClientFactory(HttpMessageHandler handler)
    {
        _handler = handler;
    }

    public HttpClient CreateClient(string name) => new(_handler, disposeHandler: false);
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

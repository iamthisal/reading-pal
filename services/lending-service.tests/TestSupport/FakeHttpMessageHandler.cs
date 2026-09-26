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
}

using System.Net;

namespace Erp.Tests.TestSupport;

/// <summary>HttpClientFactory de test con handler configurable para mocks AEAT.</summary>
public sealed class FakeHttpClientFactory : IHttpClientFactory
{
    private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

    public FakeHttpClientFactory(Func<HttpRequestMessage, HttpResponseMessage> handler)
        => _handler = handler;

    public HttpClient CreateClient(string name)
        => new(new DelegatingHandlerImpl(_handler), disposeHandler: true);

    private sealed class DelegatingHandlerImpl : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public DelegatingHandlerImpl(Func<HttpRequestMessage, HttpResponseMessage> handler)
            => _handler = handler;

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(_handler(request));
    }

    public static FakeHttpClientFactory AeatSuccess(string estadoEnvio = "Correcto")
        => new(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(
                $"""<?xml version="1.0"?><Respuesta><EstadoEnvio>{estadoEnvio}</EstadoEnvio></Respuesta>""",
                System.Text.Encoding.UTF8,
                "application/xml"),
        });

    public static FakeHttpClientFactory AeatFailure(HttpStatusCode status = HttpStatusCode.BadRequest)
        => new(_ => new HttpResponseMessage(status)
        {
            Content = new StringContent("AEAT error", System.Text.Encoding.UTF8, "text/plain"),
        });
}

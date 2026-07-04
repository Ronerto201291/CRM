using MediatR;

namespace Erp.Tests.TestSupport;

/// <summary>
/// Graba el último request enviado y devuelve una respuesta configurada, sin
/// despachar a un handler real — evita depender de handlers complejos (p. ej.
/// CreateInvoiceHandler con hash chain/VIES) en tests unitarios de otro handler.
/// </summary>
public sealed class FakeMediator : IMediator
{
    public List<object> SentRequests { get; } = [];
    private readonly Func<object, object?> _responder;

    public FakeMediator(Func<object, object?> responder) => _responder = responder;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult((TResponse)_responder(request)!);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
    {
        SentRequests.Add(request!);
        _responder(request!);
        return Task.CompletedTask;
    }

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
    {
        SentRequests.Add(request);
        return Task.FromResult(_responder(request));
    }

    public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
        => Task.CompletedTask;

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}

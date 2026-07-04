using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;

namespace Erp.Tests.TestSupport;

public sealed class FakeEmailService : IEmailService
{
    public string? LastQuoteEmail { get; private set; }
    public string? LastPasswordResetEmail { get; private set; }
    public string? LastConfirmationEmail { get; private set; }

    public Task SendInvoiceAsync(
        string toEmail, string toName, string invoiceNumber, DateTime issueDate, DateTime dueDate,
        decimal total, string companyName, byte[]? pdfAttachment, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl, CancellationToken ct = default)
    {
        LastPasswordResetEmail = toEmail;
        return Task.CompletedTask;
    }

    public Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmUrl, CancellationToken ct = default)
    {
        LastConfirmationEmail = toEmail;
        return Task.CompletedTask;
    }

    public Task SendQuoteAsync(
        string toEmail, string toName, string quoteNumber, DateTime issueDate, DateTime validUntil,
        decimal totalAmount, string companyName, string portalUrl, byte[]? pdfAttachment = null,
        CancellationToken ct = default)
    {
        LastQuoteEmail = toEmail;
        return Task.CompletedTask;
    }

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => Task.CompletedTask;
}

public sealed class FakeQuotePdfService : IQuotePdfService
{
    public byte[] Generate(QuotePdfData data) => [0x25, 0x50, 0x44, 0x46]; // %PDF
}

public sealed class FakeInvoicePdfService : IInvoicePdfService
{
    public byte[] Generate(InvoicePdfData data) => [0x25, 0x50, 0x44, 0x46, 0x2D]; // %PDF-
}

public sealed class ConfigurableFakeMediator : IMediator
{
    private readonly Dictionary<Type, Delegate> _handlers = new();
    private readonly FakeMediator _fallback = new();

    public void Register<TRequest, TResponse>(Func<TRequest, TResponse> handler)
        where TRequest : IRequest<TResponse>
        => _handlers[typeof(TRequest)] = handler;

    public Task<TResponse> Send<TResponse>(IRequest<TResponse> request, CancellationToken cancellationToken = default)
    {
        if (_handlers.TryGetValue(request.GetType(), out var del))
            return Task.FromResult((TResponse)del.DynamicInvoke(request)!);
        return _fallback.Send(request, cancellationToken);
    }

    public Task Send<TRequest>(TRequest request, CancellationToken cancellationToken = default) where TRequest : IRequest
        => _fallback.Send(request, cancellationToken);

    public Task<object?> Send(object request, CancellationToken cancellationToken = default)
        => _fallback.Send(request, cancellationToken);

    public IAsyncEnumerable<TResponse> CreateStream<TResponse>(IStreamRequest<TResponse> request, CancellationToken cancellationToken = default)
        => _fallback.CreateStream(request, cancellationToken);

    public IAsyncEnumerable<object?> CreateStream(object request, CancellationToken cancellationToken = default)
        => _fallback.CreateStream(request, cancellationToken);

    public Task Publish(object notification, CancellationToken cancellationToken = default)
        => _fallback.Publish(notification, cancellationToken);

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
        => _fallback.Publish(notification, cancellationToken);
}


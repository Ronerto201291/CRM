using Erp.Application.Common.Interfaces;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Email noop para entorno IntegrationTests — evita conexión SMTP real en CI.
/// </summary>
public sealed class NoOpEmailService : IEmailService
{
    public Task SendInvoiceAsync(
        string toEmail, string toName, string invoiceNumber, DateTime issueDate, DateTime dueDate,
        decimal total, string companyName, byte[]? pdfAttachment, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendPasswordResetAsync(string toEmail, string toName, string resetUrl, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendEmailConfirmationAsync(string toEmail, string toName, string confirmUrl, CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendQuoteAsync(
        string toEmail, string toName, string quoteNumber, DateTime issueDate, DateTime validUntil,
        decimal totalAmount, string companyName, string portalUrl, byte[]? pdfAttachment = null,
        CancellationToken ct = default)
        => Task.CompletedTask;

    public Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
        => Task.CompletedTask;
}

namespace Erp.Application.Common.Interfaces;

public record EmailAttachment(string FileName, byte[] Content, string ContentType);

public interface IEmailService
{
    /// <summary>Sends a transactional invoice PDF to the client.</summary>
    Task SendInvoiceAsync(
        string toEmail,
        string toName,
        string invoiceNumber,
        DateTime issueDate,
        DateTime dueDate,
        decimal total,
        string companyName,
        byte[]? pdfAttachment,
        CancellationToken ct = default);

    /// <summary>Sends a password reset link.</summary>
    Task SendPasswordResetAsync(
        string toEmail,
        string toName,
        string resetUrl,
        CancellationToken ct = default);

    /// <summary>Sends an email confirmation / account activation link.</summary>
    Task SendEmailConfirmationAsync(
        string toEmail,
        string toName,
        string confirmUrl,
        CancellationToken ct = default);

    /// <summary>
    /// Sends a quote (presupuesto) to the client with portal link for online acceptance.
    /// Optionally attaches the PDF.
    /// </summary>
    Task SendQuoteAsync(
        string toEmail,
        string toName,
        string quoteNumber,
        DateTime issueDate,
        DateTime validUntil,
        decimal totalAmount,
        string companyName,
        string portalUrl,
        byte[]? pdfAttachment = null,
        CancellationToken ct = default);

    /// <summary>Sends a generic HTML email (used for fiscal reminders, alerts, etc.).</summary>
    Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default);

    /// <summary>Email genérico con adjuntos (p. ej. paquete ZIP gestoría, ADR-0018 #42e).</summary>
    Task SendWithAttachmentsAsync(
        string to,
        string subject,
        string htmlBody,
        IReadOnlyList<EmailAttachment> attachments,
        CancellationToken ct = default);
}

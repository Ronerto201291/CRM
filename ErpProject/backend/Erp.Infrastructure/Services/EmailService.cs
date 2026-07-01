using Erp.Application.Common.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Transactional email service backed by MailKit SMTP.
/// Compatible with any SMTP provider: Gmail, Outlook 365, SendGrid (smtp.sendgrid.net:587),
/// Brevo (formerly Sendinblue), Mailgun, etc.
///
/// Env var overrides (production):
///   Email__Host, Email__Port, Email__Username, Email__Password,
///   Email__FromAddress, Email__FromName, Email__AppBaseUrl
/// </summary>
public class EmailService : IEmailService
{
    private readonly EmailOptions _opt;
    private readonly ILogger<EmailService> _logger;

    public EmailService(IOptions<EmailOptions> options, ILogger<EmailService> logger)
    {
        _opt = options.Value;
        _logger = logger;
    }

    // ─── Public API ──────────────────────────────────────────────────────────────

    /// <summary>Envía un email genérico HTML (usado para recordatorios fiscales).</summary>
    public async Task SendAsync(string to, string subject, string htmlBody, CancellationToken ct = default)
    {
        await SendAsync(to, to, subject, htmlBody, ct);
    }

    public Task SendInvoiceAsync(
        string toEmail, string toName,
        string invoiceNumber, DateTime issueDate, DateTime dueDate,
        decimal total, string companyName,
        byte[]? pdfAttachment,
        CancellationToken ct = default)
    {
        var subject = $"Factura {invoiceNumber} — {companyName}";
        var html = EmailTemplates.Invoice(toName, invoiceNumber, issueDate, dueDate, total, companyName, pdfAttachment is not null);

        MimePart? attachment = null;
        if (pdfAttachment is not null)
        {
            attachment = new MimePart("application", "pdf")
            {
                Content = new MimeContent(new MemoryStream(pdfAttachment)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = $"Factura_{invoiceNumber.Replace("/", "-")}.pdf"
            };
        }

        return SendAsync(toEmail, toName, subject, html, ct, attachment);
    }

    public Task SendPasswordResetAsync(
        string toEmail, string toName, string resetUrl, CancellationToken ct = default)
    {
        var subject = "Restablecer contraseña — ERP";
        var html = EmailTemplates.PasswordReset(toName, resetUrl, _opt.FromName);
        return SendAsync(toEmail, toName, subject, html, ct);
    }

    public Task SendEmailConfirmationAsync(
        string toEmail, string toName, string confirmUrl, CancellationToken ct = default)
    {
        var subject = "Confirma tu dirección de correo — ERP";
        var html = EmailTemplates.EmailConfirmation(toName, confirmUrl, _opt.FromName);
        return SendAsync(toEmail, toName, subject, html, ct);
    }

    public Task SendQuoteAsync(
        string toEmail, string toName,
        string quoteNumber, DateTime issueDate, DateTime validUntil,
        decimal totalAmount, string companyName, string portalUrl,
        byte[]? pdfAttachment = null,
        CancellationToken ct = default)
    {
        var subject = $"Presupuesto {quoteNumber} — {companyName}";
        var html = EmailTemplates.Quote(toName, quoteNumber, issueDate, validUntil, totalAmount, companyName, portalUrl, pdfAttachment is not null);

        MimePart? attachment = null;
        if (pdfAttachment is not null)
        {
            attachment = new MimePart("application", "pdf")
            {
                Content = new MimeContent(new MemoryStream(pdfAttachment)),
                ContentDisposition = new ContentDisposition(ContentDisposition.Attachment),
                ContentTransferEncoding = ContentEncoding.Base64,
                FileName = $"Presupuesto_{quoteNumber.Replace("/", "-")}.pdf"
            };
        }

        return SendAsync(toEmail, toName, subject, html, ct, attachment);
    }

    // ─── Core Send ───────────────────────────────────────────────────────────────

    private async Task SendAsync(
        string toEmail, string toName,
        string subject, string htmlBody,
        CancellationToken ct,
        MimePart? attachment = null)
    {
        var message = BuildMessage(toEmail, toName, subject, htmlBody, attachment);

        try
        {
            using var client = new SmtpClient();

            var socketOptions = _opt.UseSsl
                ? SecureSocketOptions.SslOnConnect       // port 465
                : SecureSocketOptions.StartTlsWhenAvailable; // port 587 (STARTTLS)

            await client.ConnectAsync(_opt.Host, _opt.Port, socketOptions, ct);
            await client.AuthenticateAsync(_opt.Username, _opt.Password, ct);
            await client.SendAsync(message, ct);
            await client.DisconnectAsync(true, ct);

            _logger.LogInformation("Email sent to {Email} | subject: {Subject}", toEmail, subject);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send email to {Email} | subject: {Subject}", toEmail, subject);
            throw;
        }
    }

    private MimeMessage BuildMessage(
        string toEmail, string toName,
        string subject, string htmlBody,
        MimePart? attachment)
    {
        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(_opt.FromName, _opt.FromAddress));
        message.To.Add(new MailboxAddress(toName, toEmail));
        message.Subject = subject;

        var bodyPart = new TextPart("html") { Text = htmlBody };

        if (attachment is null)
        {
            message.Body = bodyPart;
        }
        else
        {
            var multipart = new Multipart("mixed");
            multipart.Add(bodyPart);
            multipart.Add(attachment);
            message.Body = multipart;
        }

        return message;
    }
}

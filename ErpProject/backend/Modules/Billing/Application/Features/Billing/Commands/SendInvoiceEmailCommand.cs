using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Application.Features.Billing.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Billing.Commands;

public record SendInvoiceEmailCommand(Guid InvoiceId) : IRequest<SendInvoiceEmailResult>;

public record SendInvoiceEmailResult(bool Sent, string RecipientEmail, string InvoiceNumber);

public class SendInvoiceEmailHandler : IRequestHandler<SendInvoiceEmailCommand, SendInvoiceEmailResult>
{
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _appDb;
    private readonly IClientInfoService _clientInfo;
    private readonly IEmailService _email;
    private readonly IMediator _mediator;

    public SendInvoiceEmailHandler(
        IBillingDbContext billing,
        IApplicationDbContext appDb,
        IClientInfoService clientInfo,
        IEmailService email,
        IMediator mediator)
    {
        _billing = billing;
        _appDb = appDb;
        _clientInfo = clientInfo;
        _email = email;
        _mediator = mediator;
    }

    public async Task<SendInvoiceEmailResult> Handle(SendInvoiceEmailCommand req, CancellationToken ct)
    {
        var invoice = await _billing.Invoices
            .FirstOrDefaultAsync(i => i.Id == req.InvoiceId, ct)
            ?? throw new KeyNotFoundException($"Factura '{req.InvoiceId}' no encontrada.");

        if (!invoice.IsLocked)
            throw new InvalidOperationException(
                "Solo se pueden enviar por email facturas bloqueadas (estado 'Locked'). " +
                "Bloquee la factura antes de enviarla.");

        // Resolver datos de empresa
        var company = await _appDb.Companies
            .Where(c => c.Id == invoice.CompanyId)
            .Select(c => new { c.Name })
            .FirstOrDefaultAsync(ct);
        var companyName = company?.Name ?? "ERP";

        // Resolver email y nombre del destinatario:
        // - Clientes registrados → IClientInfoService (CRM)
        // - Clientes manuales/B2C → snapshot fiscal de la factura
        string recipientEmail;
        string recipientName;

        if (invoice.ClientId.HasValue && invoice.ClientType == "Registered")
        {
            var client = await _clientInfo.GetByIdAsync(invoice.ClientId.Value, ct)
                ?? throw new InvalidOperationException(
                    $"No se encontraron datos del cliente '{invoice.ClientId}' para enviar la factura.");

            if (string.IsNullOrWhiteSpace(client.Email))
                throw new InvalidOperationException(
                    $"El cliente '{client.Name}' no tiene dirección de correo electrónico registrada.");

            recipientEmail = client.Email;
            recipientName  = client.Name;
        }
        else
        {
            // Manual / B2C: usar email del snapshot de la factura
            if (string.IsNullOrWhiteSpace(invoice.ClientEmail))
                throw new InvalidOperationException(
                    "La factura no tiene dirección de correo electrónico del cliente. " +
                    "Edite la factura y añada el email del destinatario.");

            recipientEmail = invoice.ClientEmail;
            recipientName  = invoice.ClientName ?? "Cliente";
        }

        // Generar PDF (reutiliza caché MinIO si ya existe)
        var pdfResult = await _mediator.Send(new GetInvoicePdfQuery(invoice.Id), ct);

        await _email.SendInvoiceAsync(
            toEmail: recipientEmail,
            toName: recipientName,
            invoiceNumber: invoice.Number,
            issueDate: invoice.IssueDate,
            dueDate: invoice.DueDate,
            total: invoice.Total,
            companyName: companyName,
            pdfAttachment: pdfResult.PdfBytes,
            ct: ct);

        return new SendInvoiceEmailResult(true, recipientEmail, invoice.Number);
    }
}

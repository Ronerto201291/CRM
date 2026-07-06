using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Billing.Commands;

/// <summary>Inicia un pago Stripe puntual desde el portal público por token (ADR-0018 #39).</summary>
public class CreateInvoiceCheckoutSessionCommand : IRequest<string>
{
    public string Token { get; set; } = string.Empty;
}

public class CreateInvoiceCheckoutSessionHandler : IRequestHandler<CreateInvoiceCheckoutSessionCommand, string>
{
    private readonly IBillingDbContext _ctx;
    private readonly IInvoicePaymentGateway _gateway;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public CreateInvoiceCheckoutSessionHandler(
        IBillingDbContext ctx, IInvoicePaymentGateway gateway, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _gateway = gateway;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<string> Handle(CreateInvoiceCheckoutSessionCommand req, CancellationToken ct)
    {
        var invoice = await _ctx.Invoices
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(i => i.PublicViewToken == req.Token, ct)
            ?? throw new KeyNotFoundException("Factura no encontrada o token inválido.");

        if (!invoice.IsLocked)
            throw new InvalidOperationException(
                "Solo se pueden pagar facturas emitidas (bloqueadas conforme a la Ley Antifraude).");

        if (invoice.Status == "Paid")
            throw new InvalidOperationException("Esta factura ya está pagada.");

        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        var successUrl = $"{portalBaseUrl}/factura/{req.Token}?pago=exito";
        var cancelUrl = $"{portalBaseUrl}/factura/{req.Token}?pago=cancelado";

        return await _gateway.CreateInvoiceCheckoutSessionAsync(
            invoice.Id, invoice.Number, invoice.Total, "eur", successUrl, cancelUrl, ct);
    }
}

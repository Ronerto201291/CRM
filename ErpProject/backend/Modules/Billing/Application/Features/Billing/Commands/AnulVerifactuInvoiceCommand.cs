using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Billing.Commands;

/// <summary>Encola anulación VERI*FACTU de una factura ya enviada a AEAT.</summary>
public class AnulVerifactuInvoiceCommand : IRequest<bool>
{
    public Guid InvoiceId { get; set; }
}

public class AnulVerifactuInvoiceHandler : IRequestHandler<AnulVerifactuInvoiceCommand, bool>
{
    private readonly IBillingDbContext _ctx;
    private readonly IVerifactuSubmissionGateway _gateway;

    public AnulVerifactuInvoiceHandler(IBillingDbContext ctx, IVerifactuSubmissionGateway gateway)
    {
        _ctx = ctx;
        _gateway = gateway;
    }

    public async Task<bool> Handle(AnulVerifactuInvoiceCommand request, CancellationToken ct)
    {
        var inv = await _ctx.Invoices.FirstOrDefaultAsync(i => i.Id == request.InvoiceId, ct);
        if (inv == null) return false;
        if (inv.VerifactuSubmittedAt is null && inv.VerifactuRealtimeSubmission)
            throw new InvalidOperationException("La factura no fue enviada a VeriFactu — no requiere anulación.");
        if (string.IsNullOrEmpty(inv.VerifactuHuella))
            throw new InvalidOperationException("La factura no tiene huella VeriFactu.");

        _gateway.EnqueueVerifactuAnulacion(inv.Id);
        return true;
    }
}

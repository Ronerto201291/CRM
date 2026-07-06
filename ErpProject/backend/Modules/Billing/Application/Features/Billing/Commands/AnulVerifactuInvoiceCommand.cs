using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Features.Billing.Commands;

/// <summary>Encola anulación VERI*FACTU de una factura ya registrada.</summary>
public class AnulVerifactuInvoiceCommand : IRequest<bool>
{
    public Guid InvoiceId { get; set; }
}

public class AnulVerifactuInvoiceHandler : IRequestHandler<AnulVerifactuInvoiceCommand, bool>
{
    private readonly IVerifactuAnulacionRegistrar _registrar;

    public AnulVerifactuInvoiceHandler(IVerifactuAnulacionRegistrar registrar)
        => _registrar = registrar;

    public Task<bool> Handle(AnulVerifactuInvoiceCommand request, CancellationToken ct)
        => _registrar.RegisterAsync(request.InvoiceId, ct);
}

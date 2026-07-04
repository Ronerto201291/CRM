using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class DeleteClientHandler : IRequestHandler<DeleteClientCommand, bool>
{
    private readonly ICrmDbContext _context;
    public DeleteClientHandler(ICrmDbContext context) => _context = context;

    public async Task<bool> Handle(DeleteClientCommand request, CancellationToken ct)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (client == null) return false;

        var hasContractedServices = await _context.ClientContractedServices
            .AnyAsync(cs => cs.ClientId == request.Id, ct);
        if (hasContractedServices)
            throw new InvalidOperationException("No se puede eliminar un cliente con servicios contratados. Cancela primero sus contratos.");

        _context.Clients.Remove(client);
        await _context.SaveChangesAsync(ct);
        return true;
    }
}

/// <summary>
/// RGPD Art. 17 — Derecho de supresión.
/// Pseudoanonimiza los campos de datos personales del cliente manteniendo el registro
/// fiscal (TaxId, Id, CompanyId) requerido por obligaciones legales (AEAT, Ley 11/2021).
/// </summary>
public class AnonymizeClientHandler : IRequestHandler<AnonymizeClientCommand, bool>
{
    private readonly ICrmDbContext _context;

    public AnonymizeClientHandler(ICrmDbContext context) => _context = context;

    public async Task<bool> Handle(AnonymizeClientCommand request, CancellationToken ct)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (client == null) return false;
        if (client.IsAnonymized) return true; // idempotente

        client.Name         = "TITULAR ANÓNIMO";
        client.Email        = string.Empty;
        client.Phone        = string.Empty;
        client.Address      = string.Empty;
        client.CustomFields = "{}";
        client.IsAnonymized = true;
        client.AnonymizedAt = DateTime.UtcNow;

        _context.ActivityLogs.Add(new Erp.Modules.Crm.Domain.Entities.ActivityLog
        {
            Id          = Guid.NewGuid(),
            CompanyId   = client.CompanyId,
            EntityType  = "Client",
            EntityId    = client.Id,
            Action      = "Anonymized",
            Description = "Datos personales pseudoanonimizados (RGPD Art. 17)"
        });

        await _context.SaveChangesAsync(ct);
        return true;
    }
}

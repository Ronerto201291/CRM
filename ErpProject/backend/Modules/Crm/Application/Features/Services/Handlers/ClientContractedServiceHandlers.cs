using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Services.Handlers;

// Guard clause inline (no FluentValidation): el precio/IVA/periodicidad final
// de un contrato se resuelven aquí tras aplicar los overrides opcionales del
// command sobre los valores del catálogo — un validator de input no puede
// comprobar esto sin duplicar la resolución de overrides.
internal static class ServiceCatalogGuards
{
    public static void Validate(string name, decimal price, decimal taxRate, string periodicity)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new InvalidOperationException("El nombre del servicio es obligatorio.");
        if (price < 0)
            throw new InvalidOperationException("El precio no puede ser negativo.");
        if (taxRate < 0 || taxRate > 100)
            throw new InvalidOperationException("El tipo de IVA debe estar entre 0 y 100.");
        if (!ServicePeriodicity.IsValid(periodicity))
            throw new InvalidOperationException($"Periodicidad no válida: {periodicity}");
    }
}

public class CreateClientContractedServiceHandler : IRequestHandler<CreateClientContractedServiceCommand, ClientContractedServiceDto>
{
    private readonly ICrmDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CreateClientContractedServiceHandler(ICrmDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<ClientContractedServiceDto> Handle(CreateClientContractedServiceCommand request, CancellationToken ct)
    {
        var client = await _context.Clients.FirstOrDefaultAsync(c => c.Id == request.ClientId, ct)
            ?? throw new InvalidOperationException("Cliente no encontrado.");
        var catalogItem = await _context.ServiceCatalogItems
            .FirstOrDefaultAsync(s => s.Id == request.ServiceCatalogItemId && s.IsActive, ct)
            ?? throw new InvalidOperationException("Servicio del catálogo no encontrado o inactivo.");

        var price = request.PriceOverride ?? catalogItem.DefaultPrice;
        var taxRate = request.TaxRateOverride ?? catalogItem.DefaultTaxRate;
        var periodicity = request.PeriodicityOverride ?? catalogItem.DefaultPeriodicity;
        ServiceCatalogGuards.Validate(catalogItem.Name, price, taxRate, periodicity);

        var companyId = _tenantContext.TenantId ?? Guid.Empty;
        var contract = new ClientContractedService
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            ClientId = request.ClientId,
            ServiceCatalogItemId = request.ServiceCatalogItemId,
            ServiceName = catalogItem.Name,
            Price = price,
            TaxRate = taxRate,
            Periodicity = periodicity,
            StartDate = request.StartDate,
            NextBillingDate = request.StartDate,
            Status = "Active",
        };
        _context.ClientContractedServices.Add(contract);
        await _context.SaveChangesAsync(ct);

        return ToDto(contract);
    }

    internal static ClientContractedServiceDto ToDto(ClientContractedService c) => new()
    {
        Id = c.Id,
        ClientId = c.ClientId,
        ServiceCatalogItemId = c.ServiceCatalogItemId,
        ServiceName = c.ServiceName,
        Price = c.Price,
        TaxRate = c.TaxRate,
        Periodicity = c.Periodicity,
        StartDate = c.StartDate,
        NextBillingDate = c.NextBillingDate,
        EndDate = c.EndDate,
        Status = c.Status,
        LastInvoiceId = c.LastInvoiceId,
        CreatedAt = c.CreatedAt,
    };
}

public class CancelClientContractedServiceHandler : IRequestHandler<CancelClientContractedServiceCommand, bool>
{
    private readonly ICrmDbContext _context;
    public CancelClientContractedServiceHandler(ICrmDbContext context) => _context = context;

    public async Task<bool> Handle(CancelClientContractedServiceCommand request, CancellationToken ct)
    {
        var contract = await _context.ClientContractedServices.FirstOrDefaultAsync(c => c.Id == request.Id, ct);
        if (contract == null) return false;

        contract.Status = "Cancelled";
        contract.EndDate = request.EndDate ?? DateTime.UtcNow.Date;
        contract.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return true;
    }
}

public class GetClientContractedServicesHandler : IRequestHandler<GetClientContractedServicesQuery, List<ClientContractedServiceDto>>
{
    private readonly ICrmDbContext _context;
    public GetClientContractedServicesHandler(ICrmDbContext context) => _context = context;

    public async Task<List<ClientContractedServiceDto>> Handle(GetClientContractedServicesQuery request, CancellationToken ct)
        => await _context.ClientContractedServices
            .Where(c => c.ClientId == request.ClientId)
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => new ClientContractedServiceDto
            {
                Id = c.Id,
                ClientId = c.ClientId,
                ServiceCatalogItemId = c.ServiceCatalogItemId,
                ServiceName = c.ServiceName,
                Price = c.Price,
                TaxRate = c.TaxRate,
                Periodicity = c.Periodicity,
                StartDate = c.StartDate,
                NextBillingDate = c.NextBillingDate,
                EndDate = c.EndDate,
                Status = c.Status,
                LastInvoiceId = c.LastInvoiceId,
                CreatedAt = c.CreatedAt,
            })
            .ToListAsync(ct);
}

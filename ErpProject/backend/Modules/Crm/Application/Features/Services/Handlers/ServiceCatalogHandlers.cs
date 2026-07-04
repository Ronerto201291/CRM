using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Services.Commands;
using Erp.Modules.Crm.Application.Features.Services.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Services.Handlers;

// Nombre/precio/IVA/periodicidad de CreateServiceCatalogItemCommand y
// UpdateServiceCatalogItemCommand se validan vía FluentValidation
// (Validators/ServiceCatalogValidators.cs) — CRM ya registra
// AddValidatorsFromAssembly, así que ValidationBehavior los ejecuta de
// verdad (a diferencia de otros módulos, ver ADR-0018). Para
// CreateClientContractedServiceCommand no aplica: el precio/IVA/periodicidad
// final se resuelven en el handler tras aplicar overrides sobre el catálogo,
// algo que un validator de input no puede comprobar sin duplicar esa
// resolución — por eso ese caso sigue con guard clause inline
// (ServiceCatalogGuards, en ClientContractedServiceHandlers.cs).

public class CreateServiceCatalogItemHandler : IRequestHandler<CreateServiceCatalogItemCommand, ServiceCatalogItemDto>
{
    private readonly ICrmDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CreateServiceCatalogItemHandler(ICrmDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<ServiceCatalogItemDto> Handle(CreateServiceCatalogItemCommand request, CancellationToken ct)
    {
        var item = new ServiceCatalogItem
        {
            Id = Guid.NewGuid(),
            CompanyId = _tenantContext.TenantId ?? Guid.Empty,
            Name = request.Name,
            Description = request.Description,
            DefaultPrice = request.DefaultPrice,
            DefaultTaxRate = request.DefaultTaxRate,
            DefaultPeriodicity = request.DefaultPeriodicity,
            IsActive = true,
        };
        _context.ServiceCatalogItems.Add(item);
        await _context.SaveChangesAsync(ct);

        return ToDto(item);
    }

    internal static ServiceCatalogItemDto ToDto(ServiceCatalogItem item) => new()
    {
        Id = item.Id,
        Name = item.Name,
        Description = item.Description,
        DefaultPrice = item.DefaultPrice,
        DefaultTaxRate = item.DefaultTaxRate,
        DefaultPeriodicity = item.DefaultPeriodicity,
        IsActive = item.IsActive,
        CreatedAt = item.CreatedAt,
    };
}

public class UpdateServiceCatalogItemHandler : IRequestHandler<UpdateServiceCatalogItemCommand, ServiceCatalogItemDto>
{
    private readonly ICrmDbContext _context;
    public UpdateServiceCatalogItemHandler(ICrmDbContext context) => _context = context;

    public async Task<ServiceCatalogItemDto> Handle(UpdateServiceCatalogItemCommand request, CancellationToken ct)
    {
        var item = await _context.ServiceCatalogItems.FirstOrDefaultAsync(s => s.Id == request.Id, ct)
            ?? throw new InvalidOperationException("Servicio del catálogo no encontrado.");

        item.Name = request.Name;
        item.Description = request.Description;
        item.DefaultPrice = request.DefaultPrice;
        item.DefaultTaxRate = request.DefaultTaxRate;
        item.DefaultPeriodicity = request.DefaultPeriodicity;
        item.IsActive = request.IsActive;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(ct);
        return CreateServiceCatalogItemHandler.ToDto(item);
    }
}

public class GetServiceCatalogHandler : IRequestHandler<GetServiceCatalogQuery, List<ServiceCatalogItemDto>>
{
    private readonly ICrmDbContext _context;
    public GetServiceCatalogHandler(ICrmDbContext context) => _context = context;

    public async Task<List<ServiceCatalogItemDto>> Handle(GetServiceCatalogQuery request, CancellationToken ct)
    {
        var query = _context.ServiceCatalogItems.AsQueryable();
        if (!request.IncludeInactive)
            query = query.Where(s => s.IsActive);

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new ServiceCatalogItemDto
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description,
                DefaultPrice = s.DefaultPrice,
                DefaultTaxRate = s.DefaultTaxRate,
                DefaultPeriodicity = s.DefaultPeriodicity,
                IsActive = s.IsActive,
                CreatedAt = s.CreatedAt,
            })
            .ToListAsync(ct);
    }
}

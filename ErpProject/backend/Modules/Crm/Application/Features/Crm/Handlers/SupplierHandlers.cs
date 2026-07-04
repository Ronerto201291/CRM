using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Application.Features.Crm.Handlers;

public class GetSuppliersHandler : IRequestHandler<GetSuppliersQuery, PaginatedSuppliersResult>
{
    private readonly ICrmDbContext _ctx;
    private readonly IPortalUrlProvider _portalUrlProvider;
    public GetSuppliersHandler(ICrmDbContext ctx, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<PaginatedSuppliersResult> Handle(GetSuppliersQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var query = _ctx.Suppliers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(s => s.Name.Contains(request.Search) || s.TaxId.Contains(request.Search));

        var totalCount = await query.CountAsync(ct);

        var raw = await query
            .OrderBy(s => s.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(s => new { s.Id, s.Name, s.TaxId, s.Email, s.Phone, s.Address, s.IsActive, s.CreatedAt, s.PublicUploadEnabled, s.PublicUploadToken })
            .ToListAsync(ct);

        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        var items = raw.Select(s => new SupplierDto
        {
            Id = s.Id, Name = s.Name, TaxId = s.TaxId, Email = s.Email,
            Phone = s.Phone, Address = s.Address, IsActive = s.IsActive, CreatedAt = s.CreatedAt,
            PublicUploadEnabled = s.PublicUploadEnabled,
            PublicUploadUrl = s.PublicUploadEnabled ? $"{portalBaseUrl}/proveedor/{s.PublicUploadToken}" : null,
        }).ToList();

        return new PaginatedSuppliersResult(items, totalCount, page, pageSize);
    }
}

public class GetSupplierByIdHandler : IRequestHandler<GetSupplierByIdQuery, SupplierDetailDto?>
{
    private readonly ICrmDbContext _ctx;
    private readonly IPortalUrlProvider _portalUrlProvider;
    public GetSupplierByIdHandler(ICrmDbContext ctx, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<SupplierDetailDto?> Handle(GetSupplierByIdQuery request, CancellationToken ct)
    {
        var supplier = await _ctx.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (supplier == null) return null;

        var activities = await _ctx.ActivityLogs
            .Where(a => a.EntityType == "Supplier" && a.EntityId == request.Id)
            .OrderByDescending(a => a.Timestamp)
            .Take(20)
            .Select(a => new SupplierActivityDto
            {
                Id = a.Id, EntityType = a.EntityType,
                Action = a.Action, Description = a.Description, Timestamp = a.Timestamp
            })
            .ToListAsync(ct);

        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        return new SupplierDetailDto
        {
            Id = supplier.Id, Name = supplier.Name, TaxId = supplier.TaxId,
            Email = supplier.Email, Phone = supplier.Phone, Address = supplier.Address,
            BankAccount = supplier.BankAccount, IsActive = supplier.IsActive, CreatedAt = supplier.CreatedAt,
            PublicUploadEnabled = supplier.PublicUploadEnabled,
            PublicUploadUrl = supplier.PublicUploadEnabled ? $"{portalBaseUrl}/proveedor/{supplier.PublicUploadToken}" : null,
            Activities = activities, Expenses = new()
        };
    }
}

public class CreateSupplierHandler : IRequestHandler<CreateSupplierCommand, SupplierDto>
{
    private readonly ICrmDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IPublisher _publisher;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public CreateSupplierHandler(ICrmDbContext ctx, ITenantContext tenant, IPublisher publisher, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx; _tenant = tenant; _publisher = publisher; _portalUrlProvider = portalUrlProvider;
    }

    public async Task<SupplierDto> Handle(CreateSupplierCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name = request.Name, TaxId = request.TaxId, Email = request.Email,
            Phone = request.Phone, Address = request.Address, BankAccount = request.BankAccount
        };
        _ctx.Suppliers.Add(supplier);
        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            EntityType = "Supplier", EntityId = supplier.Id,
            Action = "Created", Description = $"Proveedor {supplier.Name} creado"
        });

        await _ctx.SaveChangesAsync(ct);

        await _publisher.Publish(new SupplierCreatedEvent
        {
            SupplierId = supplier.Id, CompanyId = companyId,
            Name = supplier.Name, TaxId = supplier.TaxId,
        }, ct);

        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        return new SupplierDto
        {
            Id = supplier.Id, Name = supplier.Name, TaxId = supplier.TaxId,
            Email = supplier.Email, Phone = supplier.Phone, Address = supplier.Address,
            IsActive = supplier.IsActive, CreatedAt = supplier.CreatedAt,
            PublicUploadEnabled = supplier.PublicUploadEnabled,
            PublicUploadUrl = supplier.PublicUploadEnabled ? $"{portalBaseUrl}/proveedor/{supplier.PublicUploadToken}" : null,
        };
    }
}

public class UpdateSupplierHandler : IRequestHandler<UpdateSupplierCommand, SupplierDto?>
{
    private readonly ICrmDbContext _ctx;
    private readonly IPortalUrlProvider _portalUrlProvider;

    public UpdateSupplierHandler(ICrmDbContext ctx, IPortalUrlProvider portalUrlProvider)
    {
        _ctx = ctx;
        _portalUrlProvider = portalUrlProvider;
    }

    public async Task<SupplierDto?> Handle(UpdateSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _ctx.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (supplier == null) return null;
        if (supplier.IsAnonymized) throw new InvalidOperationException("No se puede modificar un proveedor anonimizado.");

        supplier.Name = request.Name; supplier.TaxId = request.TaxId;
        supplier.Email = request.Email; supplier.Phone = request.Phone;
        supplier.Address = request.Address; supplier.BankAccount = request.BankAccount;
        supplier.PublicUploadEnabled = request.PublicUploadEnabled;

        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = supplier.CompanyId,
            EntityType = "Supplier", EntityId = supplier.Id,
            Action = "Updated", Description = $"Proveedor {supplier.Name} actualizado"
        });

        await _ctx.SaveChangesAsync(ct);

        var portalBaseUrl = _portalUrlProvider.PortalBaseUrl.TrimEnd('/');
        return new SupplierDto
        {
            Id = supplier.Id, Name = supplier.Name, TaxId = supplier.TaxId,
            Email = supplier.Email, Phone = supplier.Phone, Address = supplier.Address,
            IsActive = supplier.IsActive, CreatedAt = supplier.CreatedAt,
            PublicUploadEnabled = supplier.PublicUploadEnabled,
            PublicUploadUrl = supplier.PublicUploadEnabled ? $"{portalBaseUrl}/proveedor/{supplier.PublicUploadToken}" : null,
        };
    }
}

/// <summary>
/// RGPD Art. 17 — Derecho de supresión.
/// Pseudoanonimiza datos personales del proveedor conservando TaxId (CIF) por obligación fiscal.
/// </summary>
public class AnonymizeSupplierHandler : IRequestHandler<AnonymizeSupplierCommand, bool>
{
    private readonly ICrmDbContext _ctx;
    public AnonymizeSupplierHandler(ICrmDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(AnonymizeSupplierCommand request, CancellationToken ct)
    {
        var supplier = await _ctx.Suppliers.FirstOrDefaultAsync(s => s.Id == request.Id, ct);
        if (supplier == null) return false;
        if (supplier.IsAnonymized) return true;

        supplier.Name        = "PROVEEDOR ANÓNIMO";
        supplier.Email       = string.Empty;
        supplier.Phone       = string.Empty;
        supplier.Address     = string.Empty;
        supplier.BankAccount = null;
        supplier.IsAnonymized = true;
        supplier.AnonymizedAt = DateTime.UtcNow;

        _ctx.ActivityLogs.Add(new ActivityLog
        {
            Id = Guid.NewGuid(), CompanyId = supplier.CompanyId,
            EntityType = "Supplier", EntityId = supplier.Id,
            Action = "Anonymized",
            Description = "Datos personales pseudoanonimizados (RGPD Art. 17)"
        });

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

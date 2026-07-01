using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Handlers;

// ── Commands ──────────────────────────────────────────────────────────────────
public record CreateCostCenterCommand(
    string Code, string Name, string Type) : IRequest<Guid>;

public record UpdateCostCenterCommand(
    Guid Id, string Name, string Type, bool IsActive) : IRequest;

public record DeleteCostCenterCommand(Guid Id) : IRequest;

// ── Queries ───────────────────────────────────────────────────────────────────
public record GetCostCentersQuery(bool? OnlyActive = null)
    : IRequest<List<CostCenterDto>>;

public record GetCostCenterQuery(Guid Id) : IRequest<CostCenterDto?>;

public record CostCenterDto(
    Guid Id, string Code, string Name, string Type,
    decimal TotalCosts, bool IsActive, DateTime CreatedAt);

// ── Handlers ──────────────────────────────────────────────────────────────────
public class CreateCostCenterHandler : IRequestHandler<CreateCostCenterCommand, Guid>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateCostCenterHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx    = ctx;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateCostCenterCommand req, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var duplicate = await _ctx.CostCenters
            .AnyAsync(c => c.CompanyId == companyId && c.Code == req.Code, ct);
        if (duplicate)
            throw new InvalidOperationException($"Ya existe un centro de coste con el código {req.Code}.");

        var cc = new CostCenter
        {
            Id        = Guid.NewGuid(),
            CompanyId = companyId,
            Code      = req.Code,
            Name      = req.Name,
            Type      = req.Type,
            IsActive  = true,
            CreatedAt = DateTime.UtcNow
        };

        _ctx.CostCenters.Add(cc);
        await _ctx.SaveChangesAsync(ct);
        return cc.Id;
    }
}

public class UpdateCostCenterHandler : IRequestHandler<UpdateCostCenterCommand>
{
    private readonly IAccountingDbContext _ctx;

    public UpdateCostCenterHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(UpdateCostCenterCommand req, CancellationToken ct)
    {
        var cc = await _ctx.CostCenters.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Centro de coste {req.Id} no encontrado.");

        cc.Name      = req.Name;
        cc.Type      = req.Type;
        cc.IsActive  = req.IsActive;
        cc.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
    }
}

public class DeleteCostCenterHandler : IRequestHandler<DeleteCostCenterCommand>
{
    private readonly IAccountingDbContext _ctx;

    public DeleteCostCenterHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task Handle(DeleteCostCenterCommand req, CancellationToken ct)
    {
        var cc = await _ctx.CostCenters.FindAsync([req.Id], ct)
            ?? throw new KeyNotFoundException($"Centro de coste {req.Id} no encontrado.");

        var hasAllocations = await _ctx.CostAllocations
            .AnyAsync(a => a.CostCenterId == req.Id, ct);
        if (hasAllocations)
            throw new InvalidOperationException(
                "No se puede eliminar un centro de coste con imputaciones contables. Desactívelo en su lugar.");

        _ctx.CostCenters.Remove(cc);
        await _ctx.SaveChangesAsync(ct);
    }
}

public class GetCostCentersHandler : IRequestHandler<GetCostCentersQuery, List<CostCenterDto>>
{
    private readonly IAccountingDbContext _ctx;

    public GetCostCentersHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<List<CostCenterDto>> Handle(GetCostCentersQuery req, CancellationToken ct)
    {
        var q = _ctx.CostCenters.AsNoTracking().AsQueryable();
        if (req.OnlyActive.HasValue) q = q.Where(c => c.IsActive == req.OnlyActive.Value);

        return await q.OrderBy(c => c.Code)
            .Select(c => new CostCenterDto(
                c.Id, c.Code, c.Name, c.Type, c.TotalCosts, c.IsActive, c.CreatedAt))
            .ToListAsync(ct);
    }
}

public class GetCostCenterHandler : IRequestHandler<GetCostCenterQuery, CostCenterDto?>
{
    private readonly IAccountingDbContext _ctx;

    public GetCostCenterHandler(IAccountingDbContext ctx) => _ctx = ctx;

    public async Task<CostCenterDto?> Handle(GetCostCenterQuery req, CancellationToken ct)
    {
        var c = await _ctx.CostCenters.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == req.Id, ct);
        if (c is null) return null;
        return new CostCenterDto(c.Id, c.Code, c.Name, c.Type, c.TotalCosts, c.IsActive, c.CreatedAt);
    }
}

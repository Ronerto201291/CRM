using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.Vat;

public record GetVatRegimesQuery : IRequest<IReadOnlyList<VatRegimeDto>>;
public record GetCurrentVatRegimeQuery : IRequest<VatRegimeDto?>;
public record SetVatRegimeCommand(string Type, DateTime EffectiveDate) : IRequest<VatRegimeDto>;

public record VatRegimeDto(
    Guid Id,
    string Type,
    bool IsActive,
    DateTime EffectiveDate);

public sealed class GetVatRegimesHandler : IRequestHandler<GetVatRegimesQuery, IReadOnlyList<VatRegimeDto>>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetVatRegimesHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<VatRegimeDto>> Handle(GetVatRegimesQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        return await _ctx.VatRegimes
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId)
            .OrderByDescending(r => r.EffectiveDate)
            .Select(r => new VatRegimeDto(r.Id, r.Type, r.IsActive, r.EffectiveDate))
            .ToListAsync(ct);
    }
}

public sealed class GetCurrentVatRegimeHandler : IRequestHandler<GetCurrentVatRegimeQuery, VatRegimeDto?>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCurrentVatRegimeHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<VatRegimeDto?> Handle(GetCurrentVatRegimeQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var regime = await _ctx.VatRegimes
            .AsNoTracking()
            .Where(r => r.CompanyId == companyId && r.IsActive)
            .OrderByDescending(r => r.EffectiveDate)
            .FirstOrDefaultAsync(ct);

        return regime is null ? null : new VatRegimeDto(regime.Id, regime.Type, regime.IsActive, regime.EffectiveDate);
    }
}

public sealed class SetVatRegimeHandler : IRequestHandler<SetVatRegimeCommand, VatRegimeDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public SetVatRegimeHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<VatRegimeDto> Handle(SetVatRegimeCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var active = await _ctx.VatRegimes
            .Where(r => r.CompanyId == companyId && r.IsActive)
            .ToListAsync(ct);
        foreach (var r in active)
            r.IsActive = false;

        var regime = new VatRegime
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            Type = request.Type,
            IsActive = true,
            EffectiveDate = request.EffectiveDate.ToUniversalTime()
        };

        _ctx.VatRegimes.Add(regime);
        await _ctx.SaveChangesAsync(ct);

        return new VatRegimeDto(regime.Id, regime.Type, regime.IsActive, regime.EffectiveDate);
    }
}

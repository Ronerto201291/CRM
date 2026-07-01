using Erp.Application.Common.Interfaces;
using Erp.Application.Features.TenantModules.Commands;
using Erp.Application.Features.TenantModules.Queries;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.TenantModules.Handlers;

public class GetTenantModulesHandler : IRequestHandler<GetTenantModulesQuery, List<TenantModuleDto>>
{
    private readonly ILicensingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetTenantModulesHandler(ILicensingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<List<TenantModuleDto>> Handle(GetTenantModulesQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        return await _ctx.TenantModules
            .Where(m => m.CompanyId == companyId)
            .OrderBy(m => m.ModuleName)
            .Select(m => new TenantModuleDto
            {
                Id = m.Id, ModuleName = m.ModuleName, IsEnabled = m.IsEnabled, CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);
    }
}

public class UpdateTenantModuleHandler : IRequestHandler<UpdateTenantModuleCommand, bool>
{
    private readonly ILicensingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateTenantModuleHandler(ILicensingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<bool> Handle(UpdateTenantModuleCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var module = await _ctx.TenantModules
            .FirstOrDefaultAsync(m => m.Id == request.Id && m.CompanyId == companyId, ct);
        if (module == null) return false;

        module.IsEnabled = request.IsEnabled;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

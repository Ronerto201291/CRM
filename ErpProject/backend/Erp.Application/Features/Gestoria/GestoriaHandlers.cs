using Erp.Application.Common.Interfaces;
using Erp.Application.DTOs;
using Erp.Application.Features.Gestoria;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Gestoria;

public record GestoriaCompanyDashboardDto(
    string CompanyId,
    string CompanyName,
    string? RoleId,
    string? RoleName,
    string? AdminRoleId,
    string? ContableRoleId,
    decimal MonthlyBilling,
    decimal MonthlyExpenses,
    int OverdueInvoices,
    int LowStockProducts,
    int PendingApprovals,
    int AlertCount);

public record GetGestoriaDashboardQuery(Guid UserId) : IRequest<IReadOnlyList<GestoriaCompanyDashboardDto>>;

public record UpdateUserCompanyRoleCommand(Guid UserId, Guid CompanyId, Guid RoleId) : IRequest<bool>;

public sealed class GetGestoriaDashboardHandler : IRequestHandler<GetGestoriaDashboardQuery, IReadOnlyList<GestoriaCompanyDashboardDto>>
{
    private readonly IApplicationDbContext _ctx;
    private readonly IGestoriaDashboardDataQuery _data;

    public GetGestoriaDashboardHandler(IApplicationDbContext ctx, IGestoriaDashboardDataQuery data)
    {
        _ctx = ctx;
        _data = data;
    }

    public async Task<IReadOnlyList<GestoriaCompanyDashboardDto>> Handle(GetGestoriaDashboardQuery request, CancellationToken ct)
    {
        var memberships = await _ctx.UserCompanies
            .IgnoreQueryFilters()
            .Include(uc => uc.Company)
            .Include(uc => uc.Role)
            .Where(uc => uc.UserId == request.UserId)
            .OrderBy(uc => uc.Company!.Name)
            .ToListAsync(ct);

        if (memberships.Count == 0)
            return [];

        var companyIds = memberships.Select(m => m.CompanyId).Distinct().ToList();
        var assignableRoles = await _ctx.Roles.IgnoreQueryFilters()
            .Where(r => companyIds.Contains(r.CompanyId) && (r.Name == "Admin" || r.Name == "Contable"))
            .ToListAsync(ct);

        var results = new List<GestoriaCompanyDashboardDto>();
        foreach (var m in memberships)
        {
            if (m.Company is null) continue;
            var kpi = await _data.GetCompanyKpiAsync(m.CompanyId, ct);
            var adminRole = assignableRoles.FirstOrDefault(r => r.CompanyId == m.CompanyId && r.Name == "Admin");
            var contableRole = assignableRoles.FirstOrDefault(r => r.CompanyId == m.CompanyId && r.Name == "Contable");
            results.Add(new GestoriaCompanyDashboardDto(
                m.CompanyId.ToString(),
                m.Company.Name,
                m.RoleId.ToString(),
                m.Role?.Name,
                adminRole?.Id.ToString(),
                contableRole?.Id.ToString(),
                kpi.MonthlyBilling,
                kpi.MonthlyExpenses,
                kpi.OverdueInvoices,
                kpi.LowStockProducts,
                kpi.PendingApprovals,
                kpi.OverdueInvoices + kpi.LowStockProducts + kpi.PendingApprovals));
        }

        return results;
    }
}

public sealed class UpdateUserCompanyRoleHandler : IRequestHandler<UpdateUserCompanyRoleCommand, bool>
{
    private readonly IApplicationDbContext _ctx;

    public UpdateUserCompanyRoleHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateUserCompanyRoleCommand request, CancellationToken ct)
    {
        var membership = await _ctx.UserCompanies.IgnoreQueryFilters()
            .FirstOrDefaultAsync(uc => uc.UserId == request.UserId && uc.CompanyId == request.CompanyId, ct);
        if (membership is null) return false;

        var roleExists = await _ctx.Roles.IgnoreQueryFilters()
            .AnyAsync(r => r.Id == request.RoleId && r.CompanyId == request.CompanyId, ct);
        if (!roleExists) throw new InvalidOperationException("Rol no válido para esta empresa.");

        membership.RoleId = request.RoleId;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

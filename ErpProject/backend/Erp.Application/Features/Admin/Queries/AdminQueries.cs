using Erp.Application.Common.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Admin.Queries;

public record AdminCompanyDto(
    Guid Id, string Name, string TaxId, bool IsActive, string Country,
    DateTime CreatedAt, string PlanName, bool SubscriptionActive);

public class GetAdminCompaniesQuery : IRequest<IReadOnlyList<AdminCompanyDto>>;

public class GetAdminCompaniesHandler : IRequestHandler<GetAdminCompaniesQuery, IReadOnlyList<AdminCompanyDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetAdminCompaniesHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<AdminCompanyDto>> Handle(GetAdminCompaniesQuery request, CancellationToken ct)
    {
        var companies = await _ctx.Companies
            .IgnoreQueryFilters()
            .OrderBy(c => c.Name)
            .Select(c => new { c.Id, c.Name, c.TaxId, c.IsActive, c.Country, c.CreatedAt, c.SubscriptionId })
            .ToListAsync(ct);

        var subIds = companies.Select(c => c.SubscriptionId).ToList();
        var subs = await _ctx.Subscriptions
            .Where(s => subIds.Contains(s.Id))
            .Select(s => new { s.Id, s.PlanName, s.IsActive })
            .ToListAsync(ct);

        var subMap = subs.ToDictionary(s => s.Id);

        return companies.Select(c =>
        {
            subMap.TryGetValue(c.SubscriptionId, out var sub);
            return new AdminCompanyDto(
                c.Id, c.Name, c.TaxId, c.IsActive, c.Country, c.CreatedAt,
                sub?.PlanName ?? "—", sub?.IsActive ?? false);
        }).ToList();
    }
}

public record AdminInvitationDto(
    Guid Id, string Email, string Token, bool IsUsed, DateTime ExpiresAt,
    Guid CompanyId, string CompanyName);

public class GetAdminInvitationsQuery : IRequest<IReadOnlyList<AdminInvitationDto>>;

public class GetAdminInvitationsHandler : IRequestHandler<GetAdminInvitationsQuery, IReadOnlyList<AdminInvitationDto>>
{
    private readonly IApplicationDbContext _ctx;

    public GetAdminInvitationsHandler(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<AdminInvitationDto>> Handle(GetAdminInvitationsQuery request, CancellationToken ct)
    {
        return await _ctx.TenantInvitations
            .IgnoreQueryFilters()
            .Include(i => i.Company)
            .OrderByDescending(i => i.ExpiresAt)
            .Select(i => new AdminInvitationDto(
                i.Id, i.Email, i.Token, i.IsUsed, i.ExpiresAt, i.CompanyId,
                i.Company != null ? i.Company.Name : "N/A"))
            .ToListAsync(ct);
    }
}

using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class CompanyMembershipLimitService(IApplicationDbContext ctx) : ICompanyMembershipLimitService
{
    public async Task<CompanyMembershipLimitResult> CheckCanAddCompanyAsync(Guid userId, CancellationToken ct = default)
    {
        var (used, max) = await GetUsageForUserAsync(userId, ct);
        if (max > 0 && used >= max)
        {
            return new CompanyMembershipLimitResult(
                false,
                used,
                max,
                $"Has alcanzado el límite de {max} empresa(s) de tu plan. Contrata el plan Gestoría para gestionar más empresas.");
        }

        return new CompanyMembershipLimitResult(true, used, max, null);
    }

    public async Task<(int CompaniesUsed, int MaxCompanies)> GetUsageForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var companiesUsed = await ctx.UserCompanies.IgnoreQueryFilters()
            .CountAsync(uc => uc.UserId == userId, ct);

        var defaultMembership = await ctx.UserCompanies.IgnoreQueryFilters()
            .Where(uc => uc.UserId == userId)
            .OrderByDescending(uc => uc.IsDefault)
            .Select(uc => uc.CompanyId)
            .FirstOrDefaultAsync(ct);

        if (defaultMembership == Guid.Empty)
        {
            var legacyCompanyId = await ctx.Users.IgnoreQueryFilters()
                .Where(u => u.Id == userId)
                .Select(u => u.CompanyId)
                .FirstOrDefaultAsync(ct);
            defaultMembership = legacyCompanyId;
        }

        if (defaultMembership == Guid.Empty)
            return (companiesUsed, 1);

        var planName = await ctx.Companies.IgnoreQueryFilters()
            .Where(c => c.Id == defaultMembership)
            .Join(ctx.Subscriptions.IgnoreQueryFilters(), c => c.SubscriptionId, s => s.Id, (_, s) => s.PlanName)
            .FirstOrDefaultAsync(ct) ?? "Free";

        var maxCompanies = await ctx.Plans.AsNoTracking()
            .Where(p => p.Name == planName)
            .Select(p => p.MaxCompanies)
            .FirstOrDefaultAsync(ct);

        if (maxCompanies <= 0)
            maxCompanies = 1;

        return (companiesUsed, maxCompanies);
    }
}

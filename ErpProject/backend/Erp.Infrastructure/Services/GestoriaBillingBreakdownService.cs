using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class GestoriaBillingBreakdownService(IApplicationDbContext ctx) : IGestoriaBillingBreakdownService
{
    public async Task<IReadOnlyList<GestoriaCompanyBillingLine>> GetCompaniesForBillingAccountAsync(
        Guid billingCompanyId, CancellationToken ct = default)
    {
        var userIds = await ctx.UserCompanies.IgnoreQueryFilters()
            .Where(uc => uc.CompanyId == billingCompanyId)
            .Select(uc => uc.UserId)
            .Distinct()
            .ToListAsync(ct);

        var legacyUserIds = await ctx.Users.IgnoreQueryFilters()
            .Where(u => u.CompanyId == billingCompanyId)
            .Select(u => u.Id)
            .ToListAsync(ct);

        var allUserIds = userIds.Concat(legacyUserIds).Distinct().ToList();
        if (allUserIds.Count == 0)
        {
            var solo = await ctx.Companies.IgnoreQueryFilters()
                .Where(c => c.Id == billingCompanyId && c.IsActive)
                .Select(c => new GestoriaCompanyBillingLine(c.Id, c.Name, c.TaxId))
                .FirstOrDefaultAsync(ct);
            return solo is null ? Array.Empty<GestoriaCompanyBillingLine>() : [solo];
        }

        var companyIds = await ctx.UserCompanies.IgnoreQueryFilters()
            .Where(uc => allUserIds.Contains(uc.UserId))
            .Select(uc => uc.CompanyId)
            .Distinct()
            .ToListAsync(ct);

        if (!companyIds.Contains(billingCompanyId))
            companyIds.Add(billingCompanyId);

        return await ctx.Companies.IgnoreQueryFilters()
            .Where(c => companyIds.Contains(c.Id) && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new GestoriaCompanyBillingLine(c.Id, c.Name, c.TaxId))
            .ToListAsync(ct);
    }
}

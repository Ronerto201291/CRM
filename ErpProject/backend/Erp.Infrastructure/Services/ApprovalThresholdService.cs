using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

public sealed class ApprovalThresholdService(IApplicationDbContext ctx) : IApprovalThresholdService
{
    public async Task<decimal> GetThresholdAsync(Guid companyId, CancellationToken ct = default)
    {
        var company = await ctx.Companies
            .AsNoTracking()
            .Where(c => c.Id == companyId)
            .Select(c => (decimal?)c.ApprovalThresholdAmount)
            .FirstOrDefaultAsync(ct);
        return company ?? 0m;
    }
}

using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Validates tenant usage against plan limits (MaxUsers, MaxInvoicesPerMonth).
/// Free plan defaults apply when no active subscription exists.
/// Enterprise (9999) is always allowed.
/// </summary>
public class PlanLimitService : IPlanLimitService
{
    private readonly IApplicationDbContext _ctx;

    // Free plan defaults (mirrors seed data in migration 20260301000000)
    private const int FreePlanMaxUsers = 1;
    private const int FreePlanMaxInvoices = 20;
    private const int Unlimited = 9999;

    public PlanLimitService(IApplicationDbContext ctx) => _ctx = ctx;

    public async Task<LimitCheckResult> CheckUserLimitAsync(Guid companyId, CancellationToken ct = default)
    {
        var (maxUsers, _) = await GetPlanLimitsAsync(companyId, ct);

        if (maxUsers == Unlimited)
            return new(true, string.Empty, 0, maxUsers);

        var userCount = await _ctx.Users
            .CountAsync(u => u.CompanyId == companyId && u.IsActive, ct);

        if (userCount >= maxUsers)
            return new(false,
                $"Límite de usuarios alcanzado ({userCount}/{maxUsers}). Actualiza tu plan para añadir más usuarios.",
                userCount, maxUsers);

        return new(true, string.Empty, userCount, maxUsers);
    }

    public async Task<LimitCheckResult> CheckInvoiceLimitAsync(
        Guid companyId, int currentMonthCount, CancellationToken ct = default)
    {
        var (_, maxInvoices) = await GetPlanLimitsAsync(companyId, ct);

        if (maxInvoices == Unlimited)
            return new(true, string.Empty, currentMonthCount, maxInvoices);

        if (currentMonthCount >= maxInvoices)
            return new(false,
                $"Límite mensual de facturas alcanzado ({currentMonthCount}/{maxInvoices}). Actualiza tu plan.",
                currentMonthCount, maxInvoices);

        return new(true, string.Empty, currentMonthCount, maxInvoices);
    }

    private async Task<(int MaxUsers, int MaxInvoices)> GetPlanLimitsAsync(Guid companyId, CancellationToken ct)
    {
        var subscription = await _ctx.Subscriptions
            .IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CompanyId == companyId && s.IsActive && s.ExpirationDate > DateTime.UtcNow, ct);

        if (subscription == null)
            return (FreePlanMaxUsers, FreePlanMaxInvoices);

        var plan = await _ctx.Plans
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Name == subscription.PlanName && p.IsActive, ct);

        if (plan == null)
            return (FreePlanMaxUsers, FreePlanMaxInvoices);

        return (plan.MaxUsers, plan.MaxInvoicesPerMonth);
    }
}

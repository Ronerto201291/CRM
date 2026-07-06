using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Licensing-scoped DbContext interface used by authorization handlers
/// to check subscriptions, plans, and per-tenant module overrides.
/// Keeps licensing concerns isolated from IApplicationDbContext.
/// </summary>
public interface ILicensingDbContext
{
    DbSet<TenantModule> TenantModules { get; }
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Plan> Plans { get; }
    DbSet<PlanModule> PlanModules { get; }
    DbSet<StripeWebhookEvent> StripeWebhookEvents { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

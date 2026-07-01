using Erp.Application.Common.Interfaces;
using Erp.Domain.Common;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Data;

/// <summary>
/// Base DbContext for per-module physical DbContexts.
/// Provides AuditableEntity timestamps and automatic CompanyId assignment from ITenantContext.
/// Each module DbContext inherits this, adds its DbSets, and applies HasQueryFilter per entity.
/// </summary>
public abstract class ModuleDbContextBase : DbContext
{
    protected readonly ITenantContext TenantContext;

    protected ModuleDbContextBase(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        foreach (var entry in ChangeTracker.Entries<AuditableEntity>())
        {
            switch (entry.State)
            {
                case EntityState.Added:
                    entry.Entity.CreatedAt = DateTime.UtcNow;
                    break;
                case EntityState.Modified:
                    entry.Entity.UpdatedAt = DateTime.UtcNow;
                    break;
            }
        }

        var tenantId = TenantContext.TenantId;
        if (tenantId.HasValue)
        {
            foreach (var entry in ChangeTracker.Entries())
            {
                if (entry.State == EntityState.Added)
                {
                    var companyIdProp = entry.Properties
                        .FirstOrDefault(p => p.Metadata.Name == "CompanyId");
                    if (companyIdProp != null && (Guid)companyIdProp.CurrentValue! == Guid.Empty)
                        companyIdProp.CurrentValue = tenantId.Value;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

using Erp.Domain.Entities.Api;
using Erp.Domain.Entities.Audit;
using Erp.Domain.Entities.Automation;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Domain.Entities.Outbox;
using Erp.Domain.Entities.Tax;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    // Core
    DbSet<Company> Companies { get; }
    DbSet<User> Users { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserPermission> UserPermissions { get; }
    DbSet<UserCompany> UserCompanies { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<TenantInvitation> TenantInvitations { get; }
    DbSet<Document> Documents { get; }

    // Tax
    DbSet<TaxReport> TaxReports { get; }
    DbSet<FiscalEvent> FiscalEvents { get; }

    // Licensing
    DbSet<Subscription> Subscriptions { get; }
    DbSet<Plan> Plans { get; }
    DbSet<PlanModule> PlanModules { get; }

    // Outbox
    DbSet<OutboxMessage> OutboxMessages { get; }

    // API
    DbSet<ApiKey> ApiKeys { get; }
    DbSet<ApiUsageLog> ApiUsageLogs { get; }

    // Audit
    DbSet<AuditLog> AuditLogs { get; }

    // Automation
    DbSet<Rule> Rules { get; }
    DbSet<Condition> Conditions { get; }
    DbSet<Erp.Domain.Entities.Automation.Action> Actions { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

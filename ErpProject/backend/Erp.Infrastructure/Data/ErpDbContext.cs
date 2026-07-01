using Erp.Domain.Common;
using Erp.Domain.Entities.Api;
using Erp.Domain.Entities.Audit;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Domain.Entities.Outbox;
using Erp.Domain.Entities.Tax;
using Erp.Domain.Entities.Automation;
using Erp.Infrastructure.Tenancy;
using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore;

// Module-owned types: referenced only to call modelBuilder.Ignore<T>() below.
// This prevents EF from generating DROP TABLE migrations for tables now managed
// by per-module DbContexts (BillingDbContext, CrmDbContext, etc.).
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Inventory.Domain.Entities;

namespace Erp.Infrastructure.Data;

/// <summary>
/// Core DbContext: manages Companies, Users, Auth, Outbox, Licensing, API keys, Audit, Automation.
/// Module tables (Billing, CRM, Inventory, Accounting, Expenses) are managed by their own
/// per-module DbContexts. They are Ignored here to prevent EF from generating DROP TABLE
/// migrations for tables it no longer owns.
/// </summary>
public class ErpDbContext : DbContext, IApplicationDbContext, ILicensingDbContext
{
    private readonly ITenantContext _tenantContext;

    public ErpDbContext(DbContextOptions<ErpDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    // Core
    public DbSet<Company> Companies { get; set; } = null!;
    public DbSet<User> Users { get; set; } = null!;
    public DbSet<Role> Roles { get; set; } = null!;
    public DbSet<Permission> Permissions { get; set; } = null!;
    public DbSet<RolePermission> RolePermissions { get; set; } = null!;
    public DbSet<UserPermission> UserPermissions { get; set; } = null!;
    public DbSet<RefreshToken> RefreshTokens { get; set; } = null!;

    // Core Modules
    public DbSet<TenantModule> TenantModules { get; set; } = null!;
    public DbSet<TenantInvitation> TenantInvitations { get; set; } = null!;

    // Tax
    public DbSet<TaxReport> TaxReports { get; set; } = null!;
    public DbSet<FiscalEvent> FiscalEvents { get; set; } = null!;

    // Licensing
    public DbSet<Subscription> Subscriptions { get; set; } = null!;
    public DbSet<Plan> Plans { get; set; } = null!;
    public DbSet<PlanModule> PlanModules { get; set; } = null!;

    // Outbox (event delivery guarantee)
    public DbSet<OutboxMessage> OutboxMessages { get; set; } = null!;

    // API
    public DbSet<ApiKey> ApiKeys { get; set; } = null!;
    public DbSet<ApiUsageLog> ApiUsageLogs { get; set; } = null!;

    // Audit
    public DbSet<AuditLog> AuditLogs { get; set; } = null!;

    // Automation
    public DbSet<Rule> Rules { get; set; } = null!;
    public DbSet<Condition> Conditions { get; set; } = null!;
    public DbSet<Erp.Domain.Entities.Automation.Action> Actions { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Ignore all module-owned entity types so EF does not generate DROP TABLE migrations.
        // These tables are now managed by per-module DbContexts.
        modelBuilder.Ignore<Invoice>();
        modelBuilder.Ignore<InvoiceLine>();
        modelBuilder.Ignore<Quote>();
        modelBuilder.Ignore<Client>();
        modelBuilder.Ignore<Lead>();
        modelBuilder.Ignore<Supplier>();
        modelBuilder.Ignore<Contact>();
        modelBuilder.Ignore<ActivityLog>();
        modelBuilder.Ignore<Account>();
        modelBuilder.Ignore<JournalEntry>();
        modelBuilder.Ignore<JournalEntryLine>();
        modelBuilder.Ignore<ExpenseUpload>();
        modelBuilder.Ignore<ExpenseDocument>();
        modelBuilder.Ignore<ExpenseDocumentLine>();
        modelBuilder.Ignore<AccountingEntry>();
        modelBuilder.Ignore<Erp.Modules.Inventory.Domain.Entities.Product>();
        modelBuilder.Ignore<Warehouse>();
        modelBuilder.Ignore<Stock>();
        modelBuilder.Ignore<StockMovement>();

        // Composite Keys
        modelBuilder.Entity<RolePermission>().HasKey(rp => new { rp.RoleId, rp.PermissionId });

        // ABAC: RolePermission
        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => rp.RoleName)
            .HasDatabaseName("IX_RolePermissions_RoleName");

        modelBuilder.Entity<RolePermission>()
            .HasIndex(rp => rp.PermissionId)
            .HasDatabaseName("IX_RolePermissions_PermissionId");

        // ABAC: Permission
        modelBuilder.Entity<Permission>()
            .HasIndex(p => new { p.Resource, p.Action })
            .IsUnique()
            .HasDatabaseName("UQ_Permissions_Resource_Action");

        // ABAC: UserPermission
        modelBuilder.Entity<UserPermission>()
            .HasIndex(up => new { up.UserId, up.PermissionId })
            .IsUnique()
            .HasDatabaseName("UQ_UserPermissions_User_Permission");

        modelBuilder.Entity<UserPermission>()
            .HasIndex(up => up.UserId)
            .HasDatabaseName("IX_UserPermissions_UserId");

        modelBuilder.Entity<UserPermission>()
            .HasIndex(up => up.PermissionId)
            .HasDatabaseName("IX_UserPermissions_PermissionId");

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.User)
            .WithMany()
            .HasForeignKey(up => up.UserId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<UserPermission>()
            .HasOne(up => up.Permission)
            .WithMany(p => p.UserPermissions)
            .HasForeignKey(up => up.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Multi-tenant Query Filters (core entities only)
        modelBuilder.Entity<User>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<Role>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<TaxReport>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<Subscription>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<ApiKey>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<Rule>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<TenantModule>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<TenantInvitation>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);
        modelBuilder.Entity<FiscalEvent>().HasQueryFilter(e => e.CompanyId == _tenantContext.TenantId);

        // JSONB columns
        modelBuilder.Entity<Subscription>().Property(e => e.ActiveModules).HasColumnType("jsonb");
        modelBuilder.Entity<AuditLog>().Property(e => e.OldValues).HasColumnType("jsonb");
        modelBuilder.Entity<AuditLog>().Property(e => e.NewValues).HasColumnType("jsonb");
        modelBuilder.Entity<Erp.Domain.Entities.Automation.Action>().Property(e => e.Configuration).HasColumnType("jsonb");

        // Indexes
        modelBuilder.Entity<AuditLog>().HasIndex(e => e.CompanyId);

        // Company public upload token unique
        modelBuilder.Entity<Company>().HasIndex(e => e.PublicUploadToken).IsUnique();

        modelBuilder.Entity<TenantInvitation>().HasIndex(e => e.Token).IsUnique();

        // Plan → PlanModule relationship
        modelBuilder.Entity<PlanModule>()
            .HasOne(pm => pm.Plan)
            .WithMany(p => p.PlanModules)
            .HasForeignKey(pm => pm.PlanId);

        // Outbox indexes
        modelBuilder.Entity<OutboxMessage>().HasIndex(e => e.Status);
        modelBuilder.Entity<OutboxMessage>().HasIndex(e => e.CreatedAt);
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

        foreach (var entry in ChangeTracker.Entries())
        {
            var tenantId = _tenantContext.TenantId;
            if (tenantId.HasValue && entry.State == EntityState.Added)
            {
                var companyIdProperty = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "CompanyId");
                if (companyIdProperty != null && (Guid)companyIdProperty.CurrentValue! == Guid.Empty)
                {
                    companyIdProperty.CurrentValue = tenantId.Value;
                }
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

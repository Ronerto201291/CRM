using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Erp.Modules.Crm.Application.Interfaces;
using Erp.Modules.Crm.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Infrastructure.Data;

public class CrmDbContext : ModuleDbContextBase, ICrmDbContext
{
    public CrmDbContext(DbContextOptions<CrmDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Client> Clients { get; set; } = null!;
    public DbSet<Lead> Leads { get; set; } = null!;
    public DbSet<Supplier> Suppliers { get; set; } = null!;
    public DbSet<Contact> Contacts { get; set; } = null!;
    public DbSet<ActivityLog> ActivityLogs { get; set; } = null!;
    public DbSet<CrmNote> Notes { get; set; } = null!;
    public DbSet<ScheduledAlert> ScheduledAlerts { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("crm");
        base.OnModelCreating(modelBuilder);

        // Multi-tenant filters
        modelBuilder.Entity<Client>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Lead>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Supplier>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Contact>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ActivityLog>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<CrmNote>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ScheduledAlert>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // Indexes
        modelBuilder.Entity<Client>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<Supplier>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<CrmNote>().HasIndex(e => new { e.EntityType, e.EntityId });
        modelBuilder.Entity<ScheduledAlert>().HasIndex(e => new { e.CompanyId, e.ScheduledAt });

        // JSONB
        modelBuilder.Entity<Client>().Property(e => e.CustomFields).HasColumnType("jsonb");

        // Contact → Client FK (optional, within CRM)
        modelBuilder.Entity<Contact>()
            .HasOne(c => c.Client)
            .WithMany()
            .HasForeignKey(c => c.ClientId)
            .OnDelete(DeleteBehavior.SetNull);

        // Contact → Supplier FK (optional, within CRM)
        modelBuilder.Entity<Contact>()
            .HasOne(c => c.Supplier)
            .WithMany()
            .HasForeignKey(c => c.SupplierId)
            .OnDelete(DeleteBehavior.SetNull);

        // ActivityLog.EntityId is polymorphic (Client, Supplier, Invoice, Expense...).
        // Ignore the Supplier.Activities navigation to prevent EF from generating a false FK.
        // Load activities via ActivityLogs.Where(a => a.EntityType == "Supplier" && a.EntityId == id).
        modelBuilder.Entity<Supplier>().Ignore(s => s.Activities);
    }
}

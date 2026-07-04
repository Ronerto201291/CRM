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
    public DbSet<ServiceCatalogItem> ServiceCatalogItems { get; set; } = null!;
    public DbSet<ClientContractedService> ClientContractedServices { get; set; } = null!;
    public DbSet<SupplierInvoiceUpload> SupplierInvoiceUploads { get; set; } = null!;

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
        modelBuilder.Entity<ServiceCatalogItem>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ClientContractedService>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<SupplierInvoiceUpload>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // Indexes
        modelBuilder.Entity<Client>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<Supplier>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<Supplier>().HasIndex(e => e.PublicUploadToken).IsUnique();
        modelBuilder.Entity<CrmNote>().HasIndex(e => new { e.EntityType, e.EntityId });
        modelBuilder.Entity<ScheduledAlert>().HasIndex(e => new { e.CompanyId, e.ScheduledAt });
        modelBuilder.Entity<ServiceCatalogItem>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<ClientContractedService>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<ClientContractedService>().HasIndex(e => e.ClientId);
        modelBuilder.Entity<ClientContractedService>().HasIndex(e => e.NextBillingDate);
        modelBuilder.Entity<SupplierInvoiceUpload>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<SupplierInvoiceUpload>().HasIndex(e => e.SupplierId);

        // SupplierInvoiceUpload → Supplier (Restrict: un proveedor con facturas subidas
        // pendientes de revisión no puede dejarse huérfano; los proveedores además nunca
        // se borran de verdad, solo se anonimizan — ver AnonymizeSupplierHandler)
        modelBuilder.Entity<SupplierInvoiceUpload>()
            .HasOne(u => u.Supplier)
            .WithMany()
            .HasForeignKey(u => u.SupplierId)
            .OnDelete(DeleteBehavior.Restrict);

        // ClientContractedService → Client (Restrict: un cliente con historial de
        // facturación por contrato no puede dejarse huérfano al borrar el cliente)
        modelBuilder.Entity<ClientContractedService>()
            .HasOne(cs => cs.Client)
            .WithMany()
            .HasForeignKey(cs => cs.ClientId)
            .OnDelete(DeleteBehavior.Restrict);

        // ClientContractedService → ServiceCatalogItem (Restrict: el catálogo se
        // desactiva, nunca se borra, así que este FK no debería poder violarse en la
        // práctica, pero se deja explícito por seguridad referencial)
        modelBuilder.Entity<ClientContractedService>()
            .HasOne(cs => cs.ServiceCatalogItem)
            .WithMany()
            .HasForeignKey(cs => cs.ServiceCatalogItemId)
            .OnDelete(DeleteBehavior.Restrict);

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

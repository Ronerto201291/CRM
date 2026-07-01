using Erp.Infrastructure.Data;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Infrastructure.Data;

public class PurchasingDbContext : ModuleDbContextBase, IPurchasingDbContext
{
    public PurchasingDbContext(DbContextOptions<PurchasingDbContext> options, Erp.Application.Common.Interfaces.ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
    public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; } = null!;
    public DbSet<GoodsReceipt> GoodsReceipts { get; set; } = null!;
    public DbSet<GoodsReceiptLine> GoodsReceiptLines { get; set; } = null!;
    public DbSet<SupplierInvoice> SupplierInvoices { get; set; } = null!;
    public DbSet<SupplierInvoiceLine> SupplierInvoiceLines { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("purchasing");
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(p => p.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<GoodsReceipt>().HasQueryFilter(g => g.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<SupplierInvoice>().HasQueryFilter(i => i.CompanyId == TenantContext.TenantId);

        modelBuilder.Entity<PurchaseOrder>().HasIndex(p => new { p.CompanyId, p.Number }).IsUnique();

        modelBuilder.Entity<PurchaseOrderLine>(e =>
        {
            e.Property(l => l.Quantity).HasPrecision(18, 4);
            e.Property(l => l.UnitPrice).HasPrecision(18, 4);
            e.HasOne<PurchaseOrder>()
                .WithMany(p => p.Lines)
                .HasForeignKey(l => l.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<GoodsReceipt>(e =>
        {
            e.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(g => g.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<GoodsReceiptLine>(e =>
        {
            e.Property(l => l.QuantityReceived).HasPrecision(18, 4);
            e.Property(l => l.UnitPrice).HasPrecision(18, 4);
            e.HasOne<GoodsReceipt>()
                .WithMany(g => g.Lines)
                .HasForeignKey(l => l.GoodsReceiptId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SupplierInvoice>(e =>
        {
            e.Property(i => i.TotalAmount).HasPrecision(18, 4);
            e.HasOne<PurchaseOrder>()
                .WithMany()
                .HasForeignKey(i => i.PurchaseOrderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<SupplierInvoiceLine>(e =>
        {
            e.Property(l => l.Quantity).HasPrecision(18, 4);
            e.Property(l => l.UnitPrice).HasPrecision(18, 4);
            e.HasOne<SupplierInvoice>()
                .WithMany(i => i.Lines)
                .HasForeignKey(l => l.SupplierInvoiceId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}

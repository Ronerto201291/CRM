using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Data;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Infrastructure.Data;

public class InventoryDbContext : ModuleDbContextBase, IInventoryDbContext
{
    public InventoryDbContext(DbContextOptions<InventoryDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Product> InventoryProducts => Set<Product>();
    public DbSet<Warehouse> Warehouses { get; set; } = null!;
    public DbSet<Stock> Stocks { get; set; } = null!;
    public DbSet<StockMovement> StockMovements { get; set; } = null!;
    public DbSet<Lot> Lots { get; set; } = null!;
    public DbSet<SerialNumber> SerialNumbers { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("inventory");
        base.OnModelCreating(modelBuilder);

        // Multi-tenant filters
        modelBuilder.Entity<Product>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Warehouse>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Stock>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<StockMovement>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Lot>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<SerialNumber>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // Performance index
        modelBuilder.Entity<Product>().HasIndex(e => e.CompanyId);

        // Stock → Product FK (within module, cascade)
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Product)
            .WithMany()
            .HasForeignKey(s => s.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stock → Warehouse FK (within module, cascade)
        modelBuilder.Entity<Stock>()
            .HasOne(s => s.Warehouse)
            .WithMany()
            .HasForeignKey(s => s.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        // Stock quantity constraint: no negative stock
        modelBuilder.Entity<Stock>()
            .ToTable(tb => tb.HasCheckConstraint("CK_Stock_Quantity_NonNegative", "\"Quantity\" >= 0"));

        // StockMovement → Product FK (within module)
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.Product)
            .WithMany()
            .HasForeignKey(m => m.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        // StockMovement → Warehouse FK (within module)
        modelBuilder.Entity<StockMovement>()
            .HasOne(m => m.Warehouse)
            .WithMany()
            .HasForeignKey(m => m.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        // StockMovement.ReferenceId is a soft cross-module FK (Invoice, Expense — no DB constraint)
    }
}

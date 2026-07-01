using Erp.Infrastructure.Data;
using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Infrastructure.Data
{
    public class SalesDbContext : ModuleDbContextBase, ISalesDbContext
    {
        public SalesDbContext(DbContextOptions<SalesDbContext> options, Erp.Application.Common.Interfaces.ITenantContext tenantContext)
            : base(options, tenantContext)
        {
        }

        public DbSet<SalesOrder> SalesOrders { get; set; } = null!;
        public DbSet<SalesOrderLine> SalesOrderLines { get; set; } = null!;
        public DbSet<DeliveryNote> DeliveryNotes { get; set; } = null!;
        public DbSet<DeliveryNoteLine> DeliveryNoteLines { get; set; } = null!;
        public DbSet<CustomerInvoice> CustomerInvoices { get; set; } = null!;
        public DbSet<CustomerInvoiceLine> CustomerInvoiceLines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.HasDefaultSchema("sales");
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<SalesOrder>().HasQueryFilter(s => s.CompanyId == TenantContext.TenantId);
            modelBuilder.Entity<DeliveryNote>().HasQueryFilter(d => d.CompanyId == TenantContext.TenantId);
            modelBuilder.Entity<CustomerInvoice>().HasQueryFilter(i => i.CompanyId == TenantContext.TenantId);

            modelBuilder.Entity<SalesOrder>().HasIndex(s => new { s.CompanyId, s.Number }).IsUnique();
        }
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Infrastructure.Data;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Expenses.Infrastructure.Data;

public class ExpensesDbContext : ModuleDbContextBase, IExpensesDbContext
{
    public ExpensesDbContext(DbContextOptions<ExpensesDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<ExpenseUpload> ExpenseUploads { get; set; } = null!;
    public DbSet<ExpenseDocument> ExpenseDocuments { get; set; } = null!;
    public DbSet<ExpenseDocumentLine> ExpenseDocumentLines { get; set; } = null!;
    public DbSet<AccountingEntry> AccountingEntries { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("expenses");
        base.OnModelCreating(modelBuilder);

        // Multi-tenant filters (ExpenseDocumentLine has no CompanyId — accessed via ExpenseDocument)
        modelBuilder.Entity<ExpenseUpload>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ExpenseDocument>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<AccountingEntry>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // Indexes
        modelBuilder.Entity<ExpenseDocument>().HasIndex(e => e.CompanyId);
        modelBuilder.Entity<ExpenseUpload>().HasIndex(e => e.CompanyId);

        // JSONB
        modelBuilder.Entity<ExpenseDocument>().Property(e => e.OcrRawData).HasColumnType("jsonb");

        // ExpenseDocument → Lines FK (cascade delete, within module)
        modelBuilder.Entity<ExpenseDocumentLine>()
            .HasOne(l => l.ExpenseDocument)
            .WithMany(d => d.Lines)
            .HasForeignKey(l => l.ExpenseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<ExpenseDocumentLine>()
            .HasIndex(l => l.ExpenseDocumentId)
            .HasDatabaseName("IX_ExpenseDocumentLines_DocumentId");

        // AccountingEntry → ExpenseDocument FK (within module)
        modelBuilder.Entity<AccountingEntry>()
            .HasOne(a => a.ExpenseDocument)
            .WithMany()
            .HasForeignKey(a => a.ExpenseDocumentId)
            .OnDelete(DeleteBehavior.Cascade);

        // ExpenseUpload → ExpenseDocument FK (optional, within module)
        modelBuilder.Entity<ExpenseUpload>()
            .HasOne(u => u.ExpenseDocument)
            .WithMany()
            .HasForeignKey(u => u.ExpenseDocumentId)
            .OnDelete(DeleteBehavior.SetNull);

        // Soft cross-module references (columns only, no EF FK constraints)
        // ExpenseDocument.SupplierId → CRM: enforced at application layer
        // ExpenseDocumentLine.ProductId → Inventory: enforced at application layer
        // ExpenseDocument.AccountingEntryId: column reference back to AccountingEntry (no nav)
    }
}

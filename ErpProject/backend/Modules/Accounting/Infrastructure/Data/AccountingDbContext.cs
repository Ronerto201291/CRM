using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Validators;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Data;

public class AccountingDbContext : ModuleDbContextBase, IAccountingDbContext
{
    public AccountingDbContext(DbContextOptions<AccountingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    public DbSet<Account> Accounts { get; set; } = null!;
    public DbSet<JournalEntry> JournalEntries { get; set; } = null!;
    public DbSet<JournalEntryLine> JournalEntryLines { get; set; } = null!;
    public DbSet<FiscalPeriod> FiscalPeriods { get; set; } = null!;
    public DbSet<Erp.Domain.Entities.Accounting.FixedAsset> FixedAssets { get; set; } = null!;
    public DbSet<DeferredEntry> DeferredEntries { get; set; } = null!;

    // Phase 0 - Compliance
    public DbSet<IvaRegister> IvaRegisters { get; set; } = null!;
    public DbSet<IvaRivaExport> IvaRivaExports { get; set; } = null!;
    public DbSet<SiiDeclaration> SiiDeclarations { get; set; } = null!;
    public DbSet<IntraEuOperation> IntraEuOperations { get; set; } = null!;
    public DbSet<Modelo347> Modelo347s { get; set; } = null!;
    public DbSet<Modelo347Record> Modelo347Records { get; set; } = null!;
    public DbSet<Modelo111And190> Modelo111And190s { get; set; } = null!;
    public DbSet<Modelo200> Modelo200s { get; set; } = null!;
    public DbSet<Modelo202> Modelo202s { get; set; } = null!;

    // Phase 2 - Accounting & Analytics
    public DbSet<CashFlowStatement> CashFlowStatements { get; set; } = null!;
    public DbSet<EquityStatement> EquityStatements { get; set; } = null!;
    public DbSet<CostCenter> CostCenters { get; set; } = null!;
    public DbSet<CostAllocation> CostAllocations { get; set; } = null!;
    public DbSet<Provision> Provisions { get; set; } = null!;
    public DbSet<DepreciationSchedule> DepreciationSchedules { get; set; } = null!;
    public DbSet<AgingReport> AgingReports { get; set; } = null!;
    public DbSet<Budget> Budgets { get; set; } = null!;
    public DbSet<BudgetLine> BudgetLines { get; set; } = null!;

    // Phase 3 - VAT & Fiscality
    public DbSet<VatTransaction> VatTransactions { get; set; } = null!;
    public DbSet<ProrrataCalculation> ProrrataCalculations { get; set; } = null!;
    public DbSet<InversionDeSujetoActivo> InversionDeSujetoActivos { get; set; } = null!;
    public DbSet<RecargoDEquivalencia> RecargoDEquivalencias { get; set; } = null!;
    public DbSet<ViesDeclaration> ViesDeclarations { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("accounting");
        base.OnModelCreating(modelBuilder);

        // Multi-tenant filters (JournalEntryLine has no CompanyId — accessed via JournalEntry)
        modelBuilder.Entity<Account>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<JournalEntry>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // FiscalPeriod: one close per (CompanyId, FiscalYear)
        modelBuilder.Entity<FiscalPeriod>(e =>
        {
            e.HasQueryFilter(p => p.CompanyId == TenantContext.TenantId);
            e.HasIndex(p => new { p.CompanyId, p.FiscalYear }).IsUnique();
        });

        // JournalEntry → JournalEntryLines FK (within module)
        modelBuilder.Entity<JournalEntryLine>()
            .HasOne(l => l.JournalEntry)
            .WithMany(e => e.JournalEntryLines)
            .HasForeignKey(l => l.JournalEntryId)
            .OnDelete(DeleteBehavior.Cascade);

        // JournalEntryLine → Account FK (within module)
        modelBuilder.Entity<JournalEntryLine>()
            .HasOne(l => l.Account)
            .WithMany()
            .HasForeignKey(l => l.AccountId)
            .OnDelete(DeleteBehavior.Restrict);

        // JournalEntry.SourceId is a soft cross-module FK (Invoice, Expense — no DB constraint)

        // ── FixedAsset ────────────────────────────────────────────────────────
        modelBuilder.Entity<Erp.Domain.Entities.Accounting.FixedAsset>(e =>
        {
            e.HasQueryFilter(a => a.CompanyId == TenantContext.TenantId);
            e.HasIndex(a => new { a.CompanyId, a.AssetCode }).IsUnique();
            e.Property(a => a.AcquisitionCost).HasPrecision(18, 2);
            e.Property(a => a.ResidualValue).HasPrecision(18, 2);
            e.Property(a => a.AccumulatedDepreciation).HasPrecision(18, 2);
            // Computed properties — not stored
            e.Ignore(a => a.DepreciableAmount);
            e.Ignore(a => a.MonthlyDepreciation);
            e.Ignore(a => a.NetBookValue);
        });

        // ── DeferredEntry ─────────────────────────────────────────────────────
        modelBuilder.Entity<DeferredEntry>(e =>
        {
            e.HasQueryFilter(d => d.CompanyId == TenantContext.TenantId);
            e.Property(d => d.TotalAmount).HasPrecision(18, 2);
            e.Property(d => d.RecognizedAmount).HasPrecision(18, 2);
            // Computed properties — not stored
            e.Ignore(d => d.RemainingAmount);
            e.Ignore(d => d.TotalMonths);
            e.Ignore(d => d.MonthlyAmount);
        });
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        // ── Guard: block posting in a closed fiscal period ────────────────────
        foreach (var entry in ChangeTracker.Entries<JournalEntry>()
            .Where(e => e.State == EntityState.Added))
        {
            var je     = entry.Entity;
            var year   = je.Date.Year;
            // Skip closing entries themselves (they are SourceType="FiscalClose")
            if (je.SourceType == "FiscalClose") continue;

            var isClosed = await FiscalPeriods
                .AnyAsync(p => p.CompanyId == je.CompanyId && p.FiscalYear == year, cancellationToken);
            if (isClosed)
                throw new InvalidOperationException(
                    $"No se puede registrar un asiento en el ejercicio fiscal {year}: período cerrado.");
        }

        // ── Validar partida doble (RD 1619/2012 - Ley Antifraude) ─────────────
        foreach (var entry in ChangeTracker.Entries<JournalEntry>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
            {
                var journalEntry = entry.Entity;
                if (journalEntry.JournalEntryLines != null && journalEntry.JournalEntryLines.Any())
                    AccountingValidator.ValidateDoubleEntry(journalEntry);
            }
        }

        foreach (var entry in ChangeTracker.Entries<JournalEntryLine>())
        {
            if (entry.State == EntityState.Added || entry.State == EntityState.Modified)
                AccountingValidator.ValidateLineBalance(entry.Entity);
        }

        return await base.SaveChangesAsync(cancellationToken);
    }
}

using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Interfaces;

public interface IAccountingDbContext
{
    // Phase 1 - Core
    DbSet<Account> Accounts { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalEntryLine> JournalEntryLines { get; }
    DbSet<FiscalPeriod> FiscalPeriods { get; }
    DbSet<Erp.Domain.Entities.Accounting.FixedAsset> FixedAssets { get; }
    DbSet<DeferredEntry> DeferredEntries { get; }

    // Phase 0 - Compliance & Legal
    DbSet<IvaRegister> IvaRegisters { get; }
    DbSet<IvaRivaExport> IvaRivaExports { get; }
    DbSet<SiiDeclaration> SiiDeclarations { get; }
    DbSet<IntraEuOperation> IntraEuOperations { get; }
    DbSet<Modelo347> Modelo347s { get; }
    DbSet<Modelo347Record> Modelo347Records { get; }
    DbSet<Modelo111And190> Modelo111And190s { get; }
    DbSet<Modelo200> Modelo200s { get; }
    DbSet<Modelo202> Modelo202s { get; }

    // Phase 2 - Accounting & Analytics
    DbSet<CashFlowStatement> CashFlowStatements { get; }
    DbSet<EquityStatement> EquityStatements { get; }
    DbSet<CostCenter> CostCenters { get; }
    DbSet<CostAllocation> CostAllocations { get; }
    DbSet<Provision> Provisions { get; }
    DbSet<DepreciationSchedule> DepreciationSchedules { get; }
    DbSet<AgingReport> AgingReports { get; }
    DbSet<Budget> Budgets { get; }
    DbSet<BudgetLine> BudgetLines { get; }

    // Phase 3 - VAT & Fiscality
    DbSet<VatTransaction> VatTransactions { get; }
    DbSet<VatRegime> VatRegimes { get; }
    DbSet<ProrrataCalculation> ProrrataCalculations { get; }
    DbSet<InversionDeSujetoActivo> InversionDeSujetoActivos { get; }
    DbSet<RecargoDEquivalencia> RecargoDEquivalencias { get; }
    DbSet<ViesDeclaration> ViesDeclarations { get; }
    DbSet<VatLiquidation> VatLiquidations { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

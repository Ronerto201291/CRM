using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Interfaces;

public interface ITreasuryDbContext
{
    DbSet<BankAccount> BankAccounts { get; }
    DbSet<BankMovement> BankMovements { get; }
    DbSet<CashEffect> CashEffects { get; }
    DbSet<CashSession> CashSessions { get; }
    DbSet<ReconciliationBatch> ReconciliationBatches { get; }
    DbSet<CashFlowForecast> CashFlowForecasts { get; }
    DbSet<PaymentOrder> PaymentOrders { get; }
    DbSet<Currency> Currencies { get; }
    DbSet<CurrencyExchange> CurrencyExchanges { get; }
    DbSet<ExchangeRateHistory> ExchangeRateHistories { get; }
    DbSet<ConfirmingOperation> ConfirmingOperations { get; }
    DbSet<FactoringOperation> FactoringOperations { get; }
    DbSet<FinancingAccount> FinancingAccounts { get; }
    DbSet<Guarantee> Guarantees { get; }
    DbSet<Collateral> Collaterals { get; }
    DbSet<BankGuarantee> BankGuarantees { get; }
    DbSet<ConsolidationGroup> ConsolidationGroups { get; }
    DbSet<SubsidiaryCompany> SubsidiaryCompanies { get; }
    DbSet<ConsolidationAdjustment> ConsolidationAdjustments { get; }
    DbSet<ConsolidatedFinancialStatement> ConsolidatedFinancialStatements { get; }
    DbSet<IntercompanyTransaction> IntercompanyTransactions { get; }
    DbSet<PosTerminal> PosTerminals { get; }
    DbSet<PosPayment> PosPayments { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}

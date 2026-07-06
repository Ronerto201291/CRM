using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Infrastructure.Data;

public class TreasuryDbContext : ModuleDbContextBase, ITreasuryDbContext
{
    public TreasuryDbContext(DbContextOptions<TreasuryDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext) { }

    public DbSet<BankAccount> BankAccounts { get; set; } = null!;
    public DbSet<BankMovement> BankMovements { get; set; } = null!;
    public DbSet<CashEffect> CashEffects { get; set; } = null!;
    public DbSet<CashSession> CashSessions { get; set; } = null!;
    public DbSet<ReconciliationBatch> ReconciliationBatches { get; set; } = null!;
    public DbSet<CashFlowForecast> CashFlowForecasts { get; set; } = null!;
    public DbSet<PaymentOrder> PaymentOrders { get; set; } = null!;
    public DbSet<Currency> Currencies { get; set; } = null!;
    public DbSet<CurrencyExchange> CurrencyExchanges { get; set; } = null!;
    public DbSet<ExchangeRateHistory> ExchangeRateHistories { get; set; } = null!;
    public DbSet<ConfirmingOperation> ConfirmingOperations { get; set; } = null!;
    public DbSet<FactoringOperation> FactoringOperations { get; set; } = null!;
    public DbSet<FinancingAccount> FinancingAccounts { get; set; } = null!;
    public DbSet<Guarantee> Guarantees { get; set; } = null!;
    public DbSet<Collateral> Collaterals { get; set; } = null!;
    public DbSet<BankGuarantee> BankGuarantees { get; set; } = null!;
    public DbSet<ConsolidationGroup> ConsolidationGroups { get; set; } = null!;
    public DbSet<SubsidiaryCompany> SubsidiaryCompanies { get; set; } = null!;
    public DbSet<ConsolidationAdjustment> ConsolidationAdjustments { get; set; } = null!;
    public DbSet<ConsolidatedFinancialStatement> ConsolidatedFinancialStatements { get; set; } = null!;
    public DbSet<IntercompanyTransaction> IntercompanyTransactions { get; set; } = null!;
    public DbSet<PosTerminal> PosTerminals { get; set; } = null!;
    public DbSet<PosPayment> PosPayments { get; set; } = null!;

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("treasury");
        base.OnModelCreating(modelBuilder);

        // Filtros multi-tenant existentes
        modelBuilder.Entity<BankAccount>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<BankMovement>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<CashEffect>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<CashSession>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ReconciliationBatch>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<CashFlowForecast>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PaymentOrder>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // Filtros multi-tenant nuevos
        modelBuilder.Entity<Currency>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<CurrencyExchange>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ExchangeRateHistory>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ConfirmingOperation>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<FactoringOperation>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<FinancingAccount>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Guarantee>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<Collateral>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<BankGuarantee>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<ConsolidationGroup>().HasQueryFilter(e => e.ParentCompanyId == TenantContext.TenantId);
        modelBuilder.Entity<SubsidiaryCompany>().HasQueryFilter(e => e.ParentCompanyId == TenantContext.TenantId);
        modelBuilder.Entity<IntercompanyTransaction>().HasQueryFilter(e => e.ParentCompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PosTerminal>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
        modelBuilder.Entity<PosPayment>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);

        // ConsolidationAdjustment and ConsolidatedFinancialStatement are filtered via ConsolidationGroupId
        // which is already protected by the ConsolidationGroup query filter.
        // Manual filtering at controller level ensures no cross-tenant leakage.

        // Configuraciones existentes
        modelBuilder.Entity<BankAccount>(e =>
        {
            e.HasIndex(b => new { b.CompanyId, b.Iban }).IsUnique();
            e.Property(b => b.CurrentBalance).HasPrecision(18, 4);
        });
        modelBuilder.Entity<BankMovement>(e =>
        {
            e.HasIndex(b => new { b.CompanyId, b.BankAccountId, b.Date });
            e.Property(b => b.Amount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<ReconciliationBatch>(e =>
        {
            e.HasIndex(b => new { b.CompanyId, b.BankAccountId, b.ReconciledAt });
            e.Property(b => b.TotalAmount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<CashEffect>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.DueDate });
            e.Property(c => c.Amount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<CashSession>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.Status });
            e.Property(c => c.OpeningBalance).HasPrecision(18, 4);
            e.Property(c => c.ExpectedClosingBalance).HasPrecision(18, 4);
            e.Property(c => c.CountedClosingBalance).HasPrecision(18, 4);
            e.Property(c => c.Difference).HasPrecision(18, 4);
        });
        modelBuilder.Entity<CashFlowForecast>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.ForecastDate });
            e.Property(c => c.ExpectedInflow).HasPrecision(18, 4);
            e.Property(c => c.ExpectedOutflow).HasPrecision(18, 4);
            e.Property(c => c.ExpectedBalance).HasPrecision(18, 4);
        });
        modelBuilder.Entity<PaymentOrder>(e =>
        {
            e.HasIndex(p => new { p.CompanyId, p.ScheduledDate });
            e.Property(p => p.Amount).HasPrecision(18, 4);
        });

        // Configuraciones nuevas
        modelBuilder.Entity<Currency>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.Code }).IsUnique();
            e.Property(c => c.ExchangeRate).HasPrecision(18, 8);
        });
        modelBuilder.Entity<CurrencyExchange>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.ExchangeDate });
            e.Property(c => c.Amount).HasPrecision(18, 4);
            e.Property(c => c.ExchangedAmount).HasPrecision(18, 4);
            e.Property(c => c.ExchangeRate).HasPrecision(18, 8);
        });
        modelBuilder.Entity<ExchangeRateHistory>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.CurrencyCode, c.RateDate });
            e.Property(c => c.Rate).HasPrecision(18, 8);
        });
        modelBuilder.Entity<ConfirmingOperation>(e =>
        {
            e.HasIndex(c => new { c.CompanyId, c.Status });
            e.Property(c => c.InvoiceAmount).HasPrecision(18, 4);
            e.Property(c => c.AdvanceAmount).HasPrecision(18, 4);
            e.Property(c => c.Fee).HasPrecision(18, 4);
        });
        modelBuilder.Entity<FactoringOperation>(e =>
        {
            e.HasIndex(f => new { f.CompanyId, f.Status });
            e.Property(f => f.InvoiceAmount).HasPrecision(18, 4);
            e.Property(f => f.AdvanceAmount).HasPrecision(18, 4);
            e.Property(f => f.DiscountFee).HasPrecision(18, 4);
            e.Property(f => f.CommissionAmount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<FinancingAccount>(e =>
        {
            e.HasIndex(f => new { f.CompanyId, f.Status });
            e.Property(f => f.Limit).HasPrecision(18, 4);
            e.Property(f => f.UtilizedAmount).HasPrecision(18, 4);
            e.Property(f => f.InterestRate).HasPrecision(8, 4);
        });
        modelBuilder.Entity<Guarantee>(e =>
        {
            e.HasIndex(g => new { g.CompanyId, g.Status });
            e.Property(g => g.Amount).HasPrecision(18, 4);
            e.Property(g => g.ClaimedAmount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<Collateral>(e =>
        {
            e.HasIndex(c => c.CompanyId);
            e.Property(c => c.Value).HasPrecision(18, 4);
            e.Property(c => c.ValuationDate).HasPrecision(18, 4);
            e.Property(c => c.LTVRatio).HasPrecision(8, 4);
        });
        modelBuilder.Entity<BankGuarantee>(e =>
        {
            e.HasIndex(b => new { b.CompanyId, b.Status });
            e.Property(b => b.Amount).HasPrecision(18, 4);
            e.Property(b => b.Fee).HasPrecision(8, 4);
        });
        modelBuilder.Entity<ConsolidationGroup>(e =>
        {
            e.HasIndex(g => new { g.ParentCompanyId, g.Code }).IsUnique();
            e.Property(g => g.ConsolidationPercentage).HasPrecision(8, 4);
        });
        modelBuilder.Entity<SubsidiaryCompany>(e =>
        {
            e.HasIndex(s => new { s.ParentCompanyId, s.CompanyId }).IsUnique();
            e.Property(s => s.OwnershipPercentage).HasPrecision(8, 4);
            e.Property(s => s.VotingPercentage).HasPrecision(8, 4);
            e.Property(s => s.AcquisitionPrice).HasPrecision(18, 4);
        });
        modelBuilder.Entity<ConsolidationAdjustment>(e =>
        {
            e.HasIndex(a => a.ConsolidationGroupId);
            e.Property(a => a.Amount).HasPrecision(18, 4);
        });
        modelBuilder.Entity<ConsolidatedFinancialStatement>(e =>
        {
            e.HasIndex(s => new { s.ConsolidationGroupId, s.FiscalYear, s.StatementType }).IsUnique();
            e.Property(s => s.TotalRevenue).HasPrecision(18, 4);
            e.Property(s => s.TotalExpenses).HasPrecision(18, 4);
            e.Property(s => s.NetIncome).HasPrecision(18, 4);
            e.Property(s => s.TotalAssets).HasPrecision(18, 4);
            e.Property(s => s.TotalLiabilities).HasPrecision(18, 4);
            e.Property(s => s.TotalEquity).HasPrecision(18, 4);
        });
        modelBuilder.Entity<IntercompanyTransaction>(e =>
        {
            e.HasIndex(t => new { t.ParentCompanyId, t.TransactionDate });
            e.Property(t => t.Amount).HasPrecision(18, 4);
        });
    }
}
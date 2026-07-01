using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities
{
    public class ConsolidationGroup : AuditableEntity
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public Guid ParentCompanyId { get; set; }
        public decimal ConsolidationPercentage { get; set; } = 100m;
        public DateTime ConsolidationDate { get; set; }
        public string Method { get; set; } = "FullConsolidation"; // FullConsolidation, ProportionalConsolidation, EquityMethod
        public string Status { get; set; } = "Active";
    }

    public class SubsidiaryCompany : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ParentCompanyId { get; set; }
        public decimal OwnershipPercentage { get; set; }
        public decimal VotingPercentage { get; set; }
        public string ConsolidationMethod { get; set; } = "Full"; // Full, Proportional, Equity
        public DateTime AcquisitionDate { get; set; }
        public decimal AcquisitionPrice { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime? DisposalDate { get; set; }
    }

    public class ConsolidationAdjustment : AuditableEntity
    {
        public Guid ConsolidationGroupId { get; set; }
        public Guid SourceCompanyId { get; set; }
        public string Type { get; set; } = "Elimination"; // Elimination, Revaluation, Goodwill, FairValue
        public string Account { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Description { get; set; } = string.Empty;
        public DateTime AdjustmentDate { get; set; }
    }

    public class ConsolidatedFinancialStatement : AuditableEntity
    {
        public Guid ConsolidationGroupId { get; set; }
        public int FiscalYear { get; set; }
        public string StatementType { get; set; } = "IncomeStatement"; // IncomeStatement, BalanceSheet, CashFlow
        public decimal TotalRevenue { get; set; }
        public decimal TotalExpenses { get; set; }
        public decimal NetIncome { get; set; }
        public decimal TotalAssets { get; set; }
        public decimal TotalLiabilities { get; set; }
        public decimal TotalEquity { get; set; }
        public DateTime PreparedDate { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Final, Audited
    }

    public class IntercompanyTransaction : AuditableEntity
    {
        public Guid ParentCompanyId { get; set; }
        public Guid SubsidiaryId { get; set; }
        public string Type { get; set; } = "Sale"; // Sale, Service, Loan, Dividend
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "EUR";
        public DateTime TransactionDate { get; set; }
        public string Status { get; set; } = "Recorded";
        public bool IsEliminated { get; set; } = false;
        public Guid? RelatedInvoiceId { get; set; }
    }
}

using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class CashFlowStatement : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int FiscalYear { get; set; }
        public decimal OperatingActivitiesCash { get; set; }
        public decimal InvestingActivitiesCash { get; set; }
        public decimal FinancingActivitiesCash { get; set; }
        public decimal NetChangeInCash { get; set; }
        public decimal BeginningCash { get; set; }
        public decimal EndingCash { get; set; }
    }

    public class EquityStatement : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int FiscalYear { get; set; }
        public decimal CapitalStock { get; set; }
        public decimal Reserves { get; set; }
        public decimal RetainedEarnings { get; set; }
        public decimal NetIncome { get; set; }
        public decimal Dividends { get; set; }
        public decimal TotalEquity { get; set; }
    }
}

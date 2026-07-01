using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class Budget : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Name { get; set; } = string.Empty;
        public int FiscalYear { get; set; }
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Approved, Active, Closed
        public ICollection<BudgetLine> Lines { get; set; } = new List<BudgetLine>();
    }

    public class BudgetLine : AuditableEntity
    {
        public Guid BudgetId { get; set; }
        public Guid? AccountId { get; set; }
        public Guid? CostCenterId { get; set; }
        public string Type { get; set; } = "Revenue"; // Revenue, Expense
        public decimal BudgetedAmount { get; set; }
        public decimal ActualAmount { get; set; }
        public decimal Variance => BudgetedAmount - ActualAmount;
        public int VariancePercentage => BudgetedAmount > 0 ? (int)((Variance / BudgetedAmount) * 100) : 0;
    }
}

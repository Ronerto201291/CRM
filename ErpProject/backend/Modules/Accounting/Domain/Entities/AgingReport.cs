using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class AgingReport : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public DateTime ReportDate { get; set; }
        public string Type { get; set; } = "Receivables"; // Receivables, Payables
        public decimal TotalAmount { get; set; }
        public decimal Current { get; set; } // 0-30 days
        public decimal Days31To60 { get; set; }
        public decimal Days61To90 { get; set; }
        public decimal Days91Plus { get; set; }
        public decimal DSO { get; set; } // Days Sales Outstanding
        public decimal DPO { get; set; } // Days Payable Outstanding
    }
}

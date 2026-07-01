using Erp.Domain.Common;

namespace Erp.Modules.Treasury.Domain.Entities
{
    public class ConfirmingOperation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid SupplierId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal AdvancePercentage { get; set; } // % avanzado
        public decimal AdvanceAmount { get; set; }
        public decimal Fee { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; } = "Active"; // Active, Paid, Cancelled
        public string FinancingProvider { get; set; } = string.Empty;
    }

    public class FactoringOperation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ClientId { get; set; }
        public Guid InvoiceId { get; set; }
        public decimal InvoiceAmount { get; set; }
        public decimal AdvancePercentage { get; set; }
        public decimal AdvanceAmount { get; set; }
        public decimal DiscountFee { get; set; } // % de descuento
        public decimal CommissionAmount { get; set; }
        public DateTime CreationDate { get; set; }
        public DateTime DueDate { get; set; }
        public DateTime? PaymentDate { get; set; }
        public string Status { get; set; } = "Active";
        public string FactoringProvider { get; set; } = string.Empty;
        public bool IsWithRecourse { get; set; } = false; // Sin recurso?
    }

    public class FinancingAccount : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "CreditLine"; // CreditLine, OverdraftFacility, Term Loan
        public decimal Limit { get; set; }
        public decimal UtilizedAmount { get; set; }
        public decimal InterestRate { get; set; }
        public string Status { get; set; } = "Active";
        public DateTime StartDate { get; set; }
        public DateTime ExpiryDate { get; set; }
        public string Provider { get; set; } = string.Empty;
    }
}

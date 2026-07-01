using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities
{
    public class SimplifiedInvoice : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid ClientId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public decimal Amount { get; set; }
        public decimal VatAmount { get; set; }
        public string VatRate { get; set; } = "21%";
        public bool IsSimplified { get; set; } = true; // Art. 15 RD 1619
        public string ClientName { get; set; } = string.Empty;
        public string ClientId_Identifier { get; set; } = string.Empty; // DNI/NIF sin validación para simplificada
        public string Description { get; set; } = string.Empty;
        public string Status { get; set; } = "Issued";
        public DateTime? CancelledDate { get; set; }
    }

    public class CreditNoteValidation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid CreditNoteId { get; set; }
        public string OriginalInvoiceNumber { get; set; } = string.Empty;
        public DateTime OriginalInvoiceDate { get; set; }
        public string Reason { get; set; } = "ErrorCorrection"; // ErrorCorrection, ReturnedMerchandise, PriceReduction, Others
        public decimal OriginalAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal VatCorrected { get; set; }
        public bool IsValid { get; set; } = true;
        public string ValidationErrors { get; set; } = string.Empty; // Art. 15 RD 1619
        public string Status { get; set; } = "Draft"; // Draft, Validated, Issued
    }

    public class InvoiceSequecing : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Series { get; set; } = "A"; // Serie de facturación
        public long LastSequenceNumber { get; set; } = 0;
        public DateTime? LastIssueDate { get; set; }
        public bool IsActive { get; set; } = true;
        public string Type { get; set; } = "Regular"; // Regular, Simplified, CreditNote
    }
}

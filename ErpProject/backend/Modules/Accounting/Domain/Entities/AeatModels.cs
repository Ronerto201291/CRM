using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class Modelo347 : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public string VatNumber { get; set; } = string.Empty;
        public string VatNumberDeclarant { get; set; } = string.Empty;
        public string PersonType { get; set; } = "Entity"; // Entity or Individual
        public string Status { get; set; } = "Draft"; // Draft, Generated, Signed, Submitted
        public DateTime? SubmissionDate { get; set; }
        public string SubmissionReference { get; set; } = string.Empty;
        public byte[] TxtFileContent { get; set; } = Array.Empty<byte>();
        public int TotalRecords { get; set; }
        public decimal TotalImported { get; set; }
        public decimal TotalInvoiced { get; set; }
    }

    public class Modelo347Record : AuditableEntity
    {
        public Guid Modelo347Id { get; set; }
        public string OperationType { get; set; } = "PurchaseOrService"; // Purchase, Service, Lease
        public string ThirdPartyVatId { get; set; } = string.Empty;
        public string ThirdPartyName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsIntraEu { get; set; } = false;
        public bool HasBankAccount { get; set; } = false;
        public bool HasCash { get; set; } = false;
    }

    public class Modelo111And190 : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string VatNumber { get; set; } = string.Empty;
        public string PersonType { get; set; } = "Entity";
        public string Status { get; set; } = "Draft";
        public decimal TotalVatOutput { get; set; }
        public decimal TotalVatInput { get; set; }
        public decimal NetVat { get; set; }
        public decimal QuotaPayable { get; set; }
        public string FormType { get; set; } = "111"; // 111, 190
        public DateTime? SubmissionDate { get; set; }
        public byte[] TxtFileContent { get; set; } = Array.Empty<byte>();
    }

    public class Modelo200 : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public string VatNumber { get; set; } = string.Empty;
        public string PersonType { get; set; } = "Entity"; // Entity, Individual/Entrepreneur
        public string Status { get; set; } = "Draft";
        public decimal TotalVatOutput { get; set; }
        public decimal TotalVatInput { get; set; }
        public decimal NetVatAnnual { get; set; }
        public decimal QuotaAnnual { get; set; }
        public bool IsModified { get; set; } = false;
        public DateTime? SubmissionDate { get; set; }
        public byte[] TxtFileContent { get; set; } = Array.Empty<byte>();
    }

    public class Modelo202 : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public string VatNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft"; // IVA devuelto (refund)
        public decimal AmountToRefund { get; set; }
        public string RefundType { get; set; } = "Quarterly"; // Quarterly, Annual, Extraordinary
        public DateTime? SubmissionDate { get; set; }
        public string SubmissionReference { get; set; } = string.Empty;
        public DateTime? RefundDate { get; set; }
        public byte[] TxtFileContent { get; set; } = Array.Empty<byte>();
    }
}

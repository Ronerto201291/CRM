using Erp.Domain.Common;

namespace Erp.Modules.Accounting.Domain.Entities
{
    public class IvaRegister : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "Purchase"; // Purchase, Sales, Intra-EU
        public Guid? InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public DateTime InvoiceDate { get; set; }
        public string SupplierVatId { get; set; } = string.Empty;
        public decimal NetAmount { get; set; }
        public decimal VatAmount { get; set; }
        public string VatRate { get; set; } = "21%";
        public string VatCode { get; set; } = string.Empty; // Art. 195-196 RIVA
        public string RecordType { get; set; } = "Regular"; // Regular, Simplified, Intra-EU
        public bool IsIntraEU { get; set; } = false;
        public bool IsReverseCharge { get; set; } = false;
        public string RivaExportStatus { get; set; } = "Pending"; // Pending, Exported
        public DateTime? RivaExportDate { get; set; }
    }

    public class IvaRivaExport : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string FileName { get; set; } = string.Empty;
        public byte[] FileContent { get; set; } = Array.Empty<byte>();
        public string Format { get; set; } = "RIVA_TXT"; // RIVA_TXT, SII_XML
        public int TotalRecords { get; set; }
        public decimal TotalNetAmount { get; set; }
        public decimal TotalVatAmount { get; set; }
        public string Status { get; set; } = "Generated"; // Generated, Validated, Submitted
        public string ValidationResult { get; set; } = string.Empty;
    }

    public class SiiDeclaration : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public string VatNumber { get; set; } = string.Empty;
        public string Status { get; set; } = "Draft"; // Draft, Generated, Signed, Submitted
        public decimal TotalVatOutput { get; set; }
        public decimal TotalVatInput { get; set; }
        public decimal NetVat { get; set; } // Output - Input
        public string SiiReference { get; set; } = string.Empty; // Referencia de presentación
        public DateTime? SubmissionDate { get; set; }
    }

    public class IntraEuOperation : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string Type { get; set; } = "Supply"; // Supply, Service, Intra-EU Sales
        public string CountryCode { get; set; } = string.Empty; // DE, FR, IT, etc.
        public string PartnerVatId { get; set; } = string.Empty;
        public Guid? InvoiceId { get; set; }
        public decimal Amount { get; set; }
        public bool IsTriangular { get; set; } = false;
        public bool HasReverseCharge { get; set; } = false;
        public string ViesStatus { get; set; } = "NotReported"; // NotReported, Reported
    }
}

using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities
{
    [Obsolete("Scaffolding sin DbSet — ADR-0005")]
    public class FacturaEDocument : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public Guid InvoiceId { get; set; }
        public string InvoiceNumber { get; set; } = string.Empty;
        public byte[] XmlContent { get; set; } = Array.Empty<byte>();
        public string FileHash { get; set; } = string.Empty; // SHA256
        public string Status { get; set; } = "Generated"; // Generated, Signed, Submitted
        public string XmlVersion { get; set; } = "3.2.2"; // FacturaE 3.2.2
        public string SignatureFormat { get; set; } = "XAdES"; // XAdES-BES o similar
        public DateTime? SignatureDate { get; set; }
        public string SignatureCertificate { get; set; } = string.Empty; // Huella digital cert.
    }

    [Obsolete("Scaffolding sin DbSet — ADR-0005")]
    public class VerifactuDeclaration : AuditableEntity
    {
        public Guid CompanyId { get; set; }
        public string NifSoftware { get; set; } = string.Empty; // RD 1007/2023
        public string SoftwareName { get; set; } = string.Empty;
        public string SoftwareVersion { get; set; } = string.Empty;
        public string SystemId { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public int TotalInvoices { get; set; }
        public decimal TotalAmount { get; set; }
        public string Status { get; set; } = "Draft"; // Draft, Generated, Signed, Submitted
        public DateTime? SubmissionDate { get; set; }
        public string SubmissionReference { get; set; } = string.Empty; // Referencia AEAT
    }

    [Obsolete("Scaffolding sin DbSet — ADR-0005")]
    public class FacturaEGraphic : AuditableEntity
    {
        public Guid FacturaEDocumentId { get; set; }
        public string PdfBase64 { get; set; } = string.Empty;
        public string QrCodeContent { get; set; } = string.Empty; // QR con info factura
        public string HtmlRepresentation { get; set; } = string.Empty;
        public string GenerationMethod { get; set; } = "Automatic"; // Automatic, Manual
        public DateTime GeneratedDate { get; set; }
        public bool IsCompliant { get; set; } = true; // RD 1007/2023 compliant?
    }
}

using Erp.Domain.Common;

namespace Erp.Modules.Expenses.Domain.Entities;

/// <summary>
/// Parsed expense data after OCR. Separate from ExpenseUpload.
/// Contains fiscal fields required by Spanish law.
/// </summary>
public class ExpenseDocument : AuditableEntity
{
    public Guid CompanyId { get; set; }

    // Source upload
    public Guid? ExpenseUploadId { get; set; }
    public ExpenseUpload? ExpenseUpload { get; set; }

    // Fiscal data (RD 1619/2012)
    public string? InvoiceNumber { get; set; }       // Nº factura proveedor
    public DateTime? IssueDate { get; set; }

    // Importes
    public decimal? TaxBase { get; set; }             // Base imponible
    public decimal? VATRate { get; set; }              // 21, 10, 4, 0
    public decimal? VATAmount { get; set; }            // Cuota IVA
    public decimal? IRPFRate { get; set; }             // IRPF profesionales (nullable)
    public decimal? IRPFAmount { get; set; }           // Retención IRPF (nullable)
    public decimal? Total { get; set; }

    // OCR raw data (JSONB)
    public string? OcrRawData { get; set; }

    // OCR confidence: 0-100 average word confidence from Tesseract hOCR.
    // Null means no OCR was run (manually entered). < 70 indicates low quality scan.
    public decimal? OcrConfidence { get; set; }

    // Supplier (parsed)
    public string? SupplierTaxId { get; set; }
    public string? SupplierName { get; set; }

    // Workflow
    public string Status { get; set; } = "Draft";     // Draft, Reviewed, PendingApproval, Approved, Rejected
    public bool IsValidated { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public bool IsLocked { get; set; }

    // Compliance (Ley Antifraude 11/2021)
    public string? HashSignature { get; set; }         // SHA256 hash del documento

    // Relations
    public Guid? SupplierId { get; set; }
    public Guid? AccountingEntryId { get; set; }       // Linked after approval

    // Line items (extracted from OCR or entered manually)
    public List<ExpenseDocumentLine> Lines { get; set; } = new();
}

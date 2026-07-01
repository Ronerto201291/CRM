using Erp.Domain.Common;

namespace Erp.Modules.Expenses.Domain.Entities;

/// <summary>
/// Raw file upload before OCR processing.
/// Separado de ExpenseDocument para mantener el flujo:
/// Upload → OCR → Borrador → Revisión → Aprobación
/// </summary>
public class ExpenseUpload : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PublicTokenUsed { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Processed, Error
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }

    // Link to processed document
    public Guid? ExpenseDocumentId { get; set; }
    public ExpenseDocument? ExpenseDocument { get; set; }
}

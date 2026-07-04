using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

/// <summary>
/// Factura subida por el proveedor vía enlace público (ADR-0018 #39). Solo captura el
/// archivo bruto para revisión manual del staff — separado de SupplierInvoice
/// (Purchasing), que exige un PurchaseOrderId y matching línea a línea; aquí no hay
/// pedido conocido en el momento de la subida (mismo motivo que ExpenseUpload vs
/// ExpenseDocument en Expenses).
/// </summary>
public class SupplierInvoiceUpload : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Guid SupplierId { get; set; }
    public Supplier? Supplier { get; set; }
    public string PublicTokenUsed { get; set; } = string.Empty;
    public string FilePath { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending"; // Pending, Reviewed
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
    public string? Comment { get; set; }
}

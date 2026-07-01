using Erp.Domain.Common;

namespace Erp.Modules.Expenses.Domain.Entities;

/// <summary>
/// Individual line item of an ExpenseDocument.
/// Extracted from OCR (heuristic parser) or entered manually.
/// </summary>
public class ExpenseDocumentLine : BaseEntity
{
    public Guid ExpenseDocumentId { get; set; }
    public ExpenseDocument? ExpenseDocument { get; set; }

    public string? Description { get; set; }   // Concepto / descripción
    public decimal Quantity    { get; set; } = 1;
    public decimal UnitPrice   { get; set; }    // Precio unitario (sin IVA)
    public decimal? VATRate    { get; set; }    // % IVA de esta línea (21, 10, 4, 0)
    public decimal LineTotal   { get; set; }    // Subtotal línea (sin IVA)

    // Optional link to Inventory product (for stock updates on approval)
    public Guid? ProductId { get; set; }

    public int SortOrder { get; set; }          // Display order
}

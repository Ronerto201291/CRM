using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

public class QuoteLine : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public int SortOrder { get; set; }

    // ── Referencia de producto (soft, con snapshot) ───────────────────────────
    public Guid? ProductId { get; set; }                      // soft ref → Inventory.Products
    public string Description { get; set; } = string.Empty;  // siempre guardado
    public string? ProductCode { get; set; }                  // snapshot del código
    public string? Unit { get; set; }                         // "ud", "hora", "kg", "m²"

    // ── Cantidades y precio ───────────────────────────────────────────────────
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }                    // 6 decimales en BD (numeric(18,6))

    // ── Descuento por línea ───────────────────────────────────────────────────
    public decimal DiscountPct { get; set; } = 0;
    public decimal DiscountAmount { get; set; } = 0;          // calculado: LineSubtotal * DiscountPct/100

    // ── IVA ───────────────────────────────────────────────────────────────────
    public decimal TaxRate { get; set; } = 21;                // 21 | 10 | 4 | 0

    // ── Totales calculados (desnormalizados) ──────────────────────────────────
    public decimal LineSubtotal { get; set; }                 // Qty * UnitPrice
    public decimal LineTaxBase { get; set; }                  // LineSubtotal - DiscountAmount
    public decimal LineTaxAmount { get; set; }                // LineTaxBase * TaxRate / 100
    public decimal LineTotalAmount { get; set; }              // LineTaxBase + LineTaxAmount
}

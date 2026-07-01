using Erp.Domain.Common;

namespace Erp.Modules.Inventory.Domain.Entities;

public class Product : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "Product"; // "Product", "Service", etc.
    
    // Costing & Pricing
    public decimal CostPrice { get; set; } = 0; // Costo Medio Ponderado calculado
    public decimal SalePrice { get; set; } = 0;
    public decimal VatPercent { get; set; } = 21;
    
    // Inventory Management
    public bool TrackStock { get; set; } = true;
    public bool IsActive { get; set; } = true;

    // Reorder management
    /// <summary>Trigger low-stock alert when stock quantity falls at or below this threshold.</summary>
    public decimal ReorderPoint { get; set; } = 0;
    /// <summary>Suggested replenishment quantity when reorder is triggered.</summary>
    public decimal ReorderQty { get; set; } = 0;
}

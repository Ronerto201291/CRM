using Erp.Domain.Common;

namespace Erp.Modules.Inventory.Domain.Entities;

/// <summary>Producto de inventario. ADR-0018 #14: migrado desde Erp.Domain.</summary>
public class Product : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Type { get; set; } = "Product";
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal VatPercent { get; set; } = 21;
    public bool TrackStock { get; set; } = true;
    public bool IsActive { get; set; } = true;
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQty { get; set; }
}

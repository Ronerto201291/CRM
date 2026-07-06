namespace Erp.Modules.Inventory.Domain.Entities;

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

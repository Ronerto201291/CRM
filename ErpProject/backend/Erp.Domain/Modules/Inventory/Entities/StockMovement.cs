namespace Erp.Modules.Inventory.Domain.Entities;

public class StockMovement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CompanyId { get; set; }
    
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }

    /// <summary>
    /// Purchase, Sale, Adjustment
    /// </summary>
    public string MovementType { get; set; } = string.Empty;

    public decimal Quantity { get; set; }
    
    /// <summary>
    /// Evaluated cost AT THE TIME of the movement. For Purchase, the purchase price. For Sale, the weighted average cost.
    /// </summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// e.g. "Invoice", "Expense", "Manual"
    /// </summary>
    public string ReferenceType { get; set; } = string.Empty;
    public Guid? ReferenceId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    
    // There is no UpdatedAt intentionally: Stock Movements are STRICTLY IMMUTABLE in this domain.
}

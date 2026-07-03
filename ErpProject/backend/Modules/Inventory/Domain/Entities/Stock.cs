using Erp.Domain.Common;

namespace Erp.Modules.Inventory.Domain.Entities;

public class Stock : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid WarehouseId { get; set; }
    public Warehouse? Warehouse { get; set; }
    public decimal Quantity { get; set; }
}

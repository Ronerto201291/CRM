using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public class StockItemDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSKU { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string ProductType { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQty { get; set; }
    public bool BelowReorderPoint { get; set; }
    public decimal StockValue { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public class StockByProductDto
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal ReorderQty { get; set; }
    public decimal TotalStock { get; set; }
    public decimal TotalValue { get; set; }
    public List<StockWarehouseItemDto> Warehouses { get; set; } = new();
}

public class StockWarehouseItemDto
{
    public Guid WarehouseId { get; set; }
    public string WarehouseName { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal StockValue { get; set; }
}

public class StockValuationResult
{
    public DateTime GeneratedAt { get; set; }
    public decimal TotalStockCost { get; set; }
    public decimal TotalStockAtSale { get; set; }
    public int TotalLines { get; set; }
    public List<StockValuationLineDto> Lines { get; set; } = new();
}

public class StockValuationLineDto
{
    public Guid ProductId { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal TotalQty { get; set; }
    public decimal TotalCostValue { get; set; }
    public decimal TotalSaleValue { get; set; }
}

public class StockMovementListDto
{
    public Guid Id { get; set; }
    public Guid ProductId { get; set; }
    public string ProductSKU { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public Guid WarehouseId { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class StockMovementsResult
{
    public int Total { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public List<StockMovementListDto> Items { get; set; } = new();
}

public class AdjustStockResult
{
    public string Message { get; set; } = string.Empty;
    public Guid MovementId { get; set; }
    public decimal NewQuantity { get; set; }
    public decimal NewCostPrice { get; set; }
    public bool BelowReorderPoint { get; set; }
}

public class GetStockQuery : IRequest<List<StockItemDto>>
{
    public Guid? WarehouseId { get; set; }
    public Guid? ProductId { get; set; }
    public bool? BelowReorderPoint { get; set; }
}

public class GetStockByProductQuery : IRequest<StockByProductDto?>
{
    public Guid ProductId { get; set; }
}

public class GetStockValuationQuery : IRequest<StockValuationResult>
{
    public Guid? WarehouseId { get; set; }
}

public class GetStockMovementsQuery : IRequest<StockMovementsResult>
{
    public Guid? ProductId { get; set; }
    public Guid? WarehouseId { get; set; }
    public string? MovementType { get; set; }
    public DateTime? From { get; set; }
    public DateTime? To { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

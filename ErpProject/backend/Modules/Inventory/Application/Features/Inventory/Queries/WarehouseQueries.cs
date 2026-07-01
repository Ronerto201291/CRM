using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public class WarehouseListDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ProductCount { get; set; }
}

public class WarehouseStockItemDto
{
    public Guid ProductId { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string ProductSKU { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal CostPrice { get; set; }
    public decimal StockValue { get; set; }
}

public class WarehouseDetailDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Location { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<WarehouseStockItemDto> Stock { get; set; } = new();
}

public class GetWarehousesQuery : IRequest<List<WarehouseListDto>>
{
    public bool? Active { get; set; }
}

public class GetWarehouseByIdQuery : IRequest<WarehouseDetailDto?>
{
    public Guid Id { get; set; }
}

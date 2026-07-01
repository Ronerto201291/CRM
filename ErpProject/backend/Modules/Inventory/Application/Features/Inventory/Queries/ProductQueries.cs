using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public class ProductListDto
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal VatPercent { get; set; }
    public bool TrackStock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public decimal TotalStock { get; set; }
}

public class ProductStockByWarehouseDto
{
    public Guid WarehouseId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
}

public class ProductDetailDto
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string Type { get; set; } = string.Empty;
    public decimal CostPrice { get; set; }
    public decimal SalePrice { get; set; }
    public decimal VatPercent { get; set; }
    public bool TrackStock { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<ProductStockByWarehouseDto> StockByWarehouse { get; set; } = new();
}

public class ProductMovementDto
{
    public Guid Id { get; set; }
    public string MovementType { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public decimal TotalCost { get; set; }
    public string? ReferenceType { get; set; }
    public Guid? ReferenceId { get; set; }
    public Guid WarehouseId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetProductsQuery : IRequest<List<ProductListDto>>
{
    public string? Search { get; set; }
    public bool? Active { get; set; }
    public string? Type { get; set; }
}

public class GetProductByIdQuery : IRequest<ProductDetailDto?>
{
    public Guid Id { get; set; }
}

public class GetProductMovementsQuery : IRequest<List<ProductMovementDto>>
{
    public Guid ProductId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

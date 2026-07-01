using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Commands;

public class CreateProductResult
{
    public Guid Id { get; set; }
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

public class CreateProductCommand : IRequest<CreateProductResult>
{
    public string SKU { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Type { get; set; }
    public decimal? CostPrice { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? VatPercent { get; set; }
    public bool? TrackStock { get; set; }
}

public class UpdateProductCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public string? SKU { get; set; }
    public string? Name { get; set; }
    public string? Description { get; set; }
    public string? Type { get; set; }
    public decimal? SalePrice { get; set; }
    public decimal? VatPercent { get; set; }
    public bool? TrackStock { get; set; }
}

public class SetProductActiveCommand : IRequest<bool>
{
    public Guid Id { get; set; }
    public bool IsActive { get; set; }
}

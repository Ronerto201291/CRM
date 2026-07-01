using MediatR;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Commands;

public class AdjustStockCommand : IRequest<AdjustStockResult>
{
    public Guid ProductId { get; set; }
    public Guid WarehouseId { get; set; }
    public decimal Quantity { get; set; }
    public decimal? UnitCost { get; set; }
    public string? Notes { get; set; }
}

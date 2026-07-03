using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public record InventoryValuationItemDto(
    Guid Id, string Name, decimal Quantity, decimal UnitCost, decimal TotalValue);

public class GetInventoryValuationQuery : IRequest<IReadOnlyList<InventoryValuationItemDto>>
{
    public string? Method { get; set; }
}

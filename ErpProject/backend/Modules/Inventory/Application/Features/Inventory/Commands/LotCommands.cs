using Erp.Modules.Inventory.Domain.Entities;
using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Commands;

public class CreateLotCommand : IRequest<Lot>
{
    public Guid ProductId { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class UpdateLotCommand : IRequest<Lot?>
{
    public Guid Id { get; set; }
    public string LotNumber { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class DeleteLotCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

using Erp.Modules.Inventory.Domain.Entities;
using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public class GetLotsQuery : IRequest<List<Lot>>;

public class GetLotByIdQuery : IRequest<Lot?>
{
    public Guid Id { get; set; }
}

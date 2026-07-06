using Erp.Modules.Inventory.Domain.Entities;
using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Queries;

public class GetSerialsQuery : IRequest<List<SerialNumber>>;

public class GetSerialByIdQuery : IRequest<SerialNumber?>
{
    public Guid Id { get; set; }
}

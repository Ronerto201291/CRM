using Erp.Modules.Inventory.Domain.Entities;
using MediatR;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Commands;

public class CreateSerialCommand : IRequest<SerialNumber>
{
    public Guid ProductId { get; set; }
    public Guid? LotId { get; set; }
    public string Serial { get; set; } = string.Empty;
}

public class UpdateSerialStatusCommand : IRequest<SerialNumber?>
{
    public Guid Id { get; set; }
    public string Status { get; set; } = string.Empty;
}

public class DeleteSerialCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

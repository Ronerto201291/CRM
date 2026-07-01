using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Commands;

public class DeleteClientCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

public class AnonymizeClientCommand : IRequest<bool>
{
    public Guid Id { get; set; }
}

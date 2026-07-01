using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public class GetContactsQuery : IRequest<List<ContactDto>>
{
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Search { get; set; }
}

public class GetContactByIdQuery : IRequest<ContactDto?>
{
    public Guid Id { get; set; }
}

using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public class GetClientsQuery : IRequest<List<ClientDto>>
{
    public string? SearchTerm { get; set; }
}

using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Services.Queries;

public class GetClientContractedServicesQuery : IRequest<List<ClientContractedServiceDto>>
{
    public Guid ClientId { get; set; }
}

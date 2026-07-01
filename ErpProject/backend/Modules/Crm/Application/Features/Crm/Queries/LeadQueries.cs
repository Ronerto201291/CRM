using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public class GetLeadsQuery : IRequest<List<LeadDto>>
{
    public string? Search { get; set; }
}

public class GetLeadByIdQuery : IRequest<LeadDto?>
{
    public Guid Id { get; set; }
}

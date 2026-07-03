using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public record PaginatedLeadsResult(
    IReadOnlyList<LeadDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetLeadsQuery : IRequest<PaginatedLeadsResult>
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetLeadByIdQuery : IRequest<LeadDto?>
{
    public Guid Id { get; set; }
}

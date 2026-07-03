using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public record PaginatedClientsResult(
    IReadOnlyList<ClientDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetClientsQuery : IRequest<PaginatedClientsResult>
{
    public string? SearchTerm { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetClientByIdQuery : IRequest<ClientDto?>
{
    public Guid Id { get; set; }
}

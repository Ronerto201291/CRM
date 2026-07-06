using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public record PaginatedContactsResult(
    IReadOnlyList<ContactDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetContactsQuery : IRequest<PaginatedContactsResult>
{
    public Guid? ClientId { get; set; }
    public Guid? SupplierId { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetContactByIdQuery : IRequest<ContactDto?>
{
    public Guid Id { get; set; }
}

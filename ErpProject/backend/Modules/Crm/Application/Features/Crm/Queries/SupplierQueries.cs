using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Crm.Application.Features.Crm.Queries;

public record PaginatedSuppliersResult(
    IReadOnlyList<SupplierDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetSuppliersQuery : IRequest<PaginatedSuppliersResult>
{
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetSupplierByIdQuery : IRequest<SupplierDetailDto?>
{
    public Guid Id { get; set; }
}

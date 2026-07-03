using Erp.Application.DTOs;
using MediatR;

namespace Erp.Modules.Billing.Application.Features.Billing.Queries;

public record PaginatedInvoicesResult(
    IReadOnlyList<InvoiceDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GetInvoicesQuery : IRequest<PaginatedInvoicesResult>
{
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetInvoiceByIdQuery : IRequest<InvoiceDto?>
{
    public Guid Id { get; set; }
}

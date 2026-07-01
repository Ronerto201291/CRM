using Erp.Modules.Sales.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Features.Invoices.Queries;

public record GetAllCustomerInvoicesQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null) : IRequest<PaginatedCustomerInvoicesResult>;

public record PaginatedCustomerInvoicesResult(
    List<CustomerInvoiceSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class CustomerInvoiceSummaryDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LineCount { get; set; }
}

public class GetAllCustomerInvoicesQueryHandler : IRequestHandler<GetAllCustomerInvoicesQuery, PaginatedCustomerInvoicesResult>
{
    private readonly ISalesDbContext _context;

    public GetAllCustomerInvoicesQueryHandler(ISalesDbContext context) => _context = context;

    public async Task<PaginatedCustomerInvoicesResult> Handle(GetAllCustomerInvoicesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.CustomerInvoices.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(i => i.Number.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(i => i.Lines)
            .OrderByDescending(i => i.InvoiceDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(i => new CustomerInvoiceSummaryDto
            {
                Id = i.Id,
                Number = i.Number,
                InvoiceDate = i.InvoiceDate,
                SubTotal = i.SubTotal,
                TaxAmount = i.TaxAmount,
                Total = i.Total,
                CreatedAt = i.CreatedAt,
                LineCount = i.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        return new PaginatedCustomerInvoicesResult(items, totalCount, request.Page, request.PageSize);
    }
}

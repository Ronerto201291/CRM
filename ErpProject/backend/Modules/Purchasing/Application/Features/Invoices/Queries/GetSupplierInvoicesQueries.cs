using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Features.Invoices.Queries;

public record GetSupplierInvoiceQuery(Guid Id) : IRequest<SupplierInvoiceDetailDto?>;

public class SupplierInvoiceDetailDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<SupplierInvoiceLineDetailDto> Lines { get; set; } = new();
}

public class SupplierInvoiceLineDetailDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class GetSupplierInvoiceQueryHandler : IRequestHandler<GetSupplierInvoiceQuery, SupplierInvoiceDetailDto?>
{
    private readonly IPurchasingDbContext _context;
    private readonly ITenantContext _tenant;

    public GetSupplierInvoiceQueryHandler(IPurchasingDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<SupplierInvoiceDetailDto?> Handle(GetSupplierInvoiceQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var invoice = await _context.SupplierInvoices
            .Include(i => i.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(i => i.Id == request.Id && i.CompanyId == tenantId, cancellationToken);
        if (invoice == null) return null;

        return new SupplierInvoiceDetailDto
        {
            Id = invoice.Id,
            PurchaseOrderId = invoice.PurchaseOrderId,
            Number = invoice.Number,
            InvoiceDate = invoice.InvoiceDate,
            TotalAmount = invoice.TotalAmount,
            CreatedAt = invoice.CreatedAt,
            Lines = invoice.Lines.Select(l => new SupplierInvoiceLineDetailDto
            {
                Id = l.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
    }
}

public record GetAllSupplierInvoicesQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null) : IRequest<PaginatedSupplierInvoicesResult>;

public record PaginatedSupplierInvoicesResult(
    List<SupplierInvoiceSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class SupplierInvoiceSummaryDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime InvoiceDate { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public decimal Subtotal { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal Total { get; set; }
    public string Status { get; set; } = "Approved";
    public DateTime CreatedAt { get; set; }
}

public class GetAllSupplierInvoicesQueryHandler : IRequestHandler<GetAllSupplierInvoicesQuery, PaginatedSupplierInvoicesResult>
{
    private readonly IPurchasingDbContext _context;
    private readonly ITenantContext _tenant;

    public GetAllSupplierInvoicesQueryHandler(IPurchasingDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<PaginatedSupplierInvoicesResult> Handle(GetAllSupplierInvoicesQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _context.SupplierInvoices
            .AsNoTracking()
            .Where(i => i.CompanyId == tenantId);

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
            .Select(i => new SupplierInvoiceSummaryDto
            {
                Id = i.Id,
                Number = i.Number,
                InvoiceDate = i.InvoiceDate,
                PurchaseOrderId = i.PurchaseOrderId,
                Subtotal = i.TotalAmount,
                TaxAmount = 0m,
                Total = i.TotalAmount,
                Status = "Approved",
                CreatedAt = i.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PaginatedSupplierInvoicesResult(items, totalCount, request.Page, request.PageSize);
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Features.Receipts.Queries;

public record GetGoodsReceiptQuery(Guid Id) : IRequest<GoodsReceiptDetailDto?>;

public class GoodsReceiptDetailDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<GoodsReceiptLineDetailDto> Lines { get; set; } = new();
}

public class GoodsReceiptLineDetailDto
{
    public Guid Id { get; set; }
    public Guid PurchaseOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
}

public class GetGoodsReceiptQueryHandler : IRequestHandler<GetGoodsReceiptQuery, GoodsReceiptDetailDto?>
{
    private readonly IPurchasingDbContext _context;
    private readonly ITenantContext _tenant;

    public GetGoodsReceiptQueryHandler(IPurchasingDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<GoodsReceiptDetailDto?> Handle(GetGoodsReceiptQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var receipt = await _context.GoodsReceipts
            .Include(r => r.Lines)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == request.Id && r.CompanyId == tenantId, cancellationToken);
        if (receipt == null) return null;

        return new GoodsReceiptDetailDto
        {
            Id = receipt.Id,
            PurchaseOrderId = receipt.PurchaseOrderId,
            Number = receipt.Number,
            ReceiptDate = receipt.ReceiptDate,
            CreatedAt = receipt.CreatedAt,
            Lines = receipt.Lines.Select(l => new GoodsReceiptLineDetailDto
            {
                Id = l.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                QuantityReceived = l.QuantityReceived,
                UnitPrice = l.UnitPrice,
            }).ToList(),
        };
    }
}

public record GetAllGoodsReceiptsQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null) : IRequest<PaginatedGoodsReceiptsResult>;

public record PaginatedGoodsReceiptsResult(
    List<GoodsReceiptSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class GoodsReceiptSummaryDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime ReceiptDate { get; set; }
    public Guid PurchaseOrderId { get; set; }
    public string Status { get; set; } = "Completed";
    public int LineCount { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class GetAllGoodsReceiptsQueryHandler : IRequestHandler<GetAllGoodsReceiptsQuery, PaginatedGoodsReceiptsResult>
{
    private readonly IPurchasingDbContext _context;
    private readonly ITenantContext _tenant;

    public GetAllGoodsReceiptsQueryHandler(IPurchasingDbContext context, ITenantContext tenant)
    {
        _context = context;
        _tenant = tenant;
    }

    public async Task<PaginatedGoodsReceiptsResult> Handle(GetAllGoodsReceiptsQuery request, CancellationToken cancellationToken)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var query = _context.GoodsReceipts
            .AsNoTracking()
            .Where(r => r.CompanyId == tenantId);

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(r => r.Number.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Include(r => r.Lines)
            .OrderByDescending(r => r.ReceiptDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(r => new GoodsReceiptSummaryDto
            {
                Id = r.Id,
                Number = r.Number,
                ReceiptDate = r.ReceiptDate,
                PurchaseOrderId = r.PurchaseOrderId,
                Status = "Completed",
                LineCount = r.Lines.Count,
                CreatedAt = r.CreatedAt,
            })
            .ToListAsync(cancellationToken);

        return new PaginatedGoodsReceiptsResult(items, totalCount, request.Page, request.PageSize);
    }
}

using Erp.Modules.Sales.Application.Interfaces;
using Erp.Modules.Sales.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Features.Deliveries.Queries;

public record GetDeliveryNoteQuery(Guid Id) : IRequest<DeliveryNoteDetailDto?>;

public class DeliveryNoteDetailDto
{
    public Guid Id { get; set; }
    public Guid CompanyId { get; set; }
    public Guid SalesOrderId { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime DeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public List<DeliveryNoteLineDto> Lines { get; set; } = new();
}

public class DeliveryNoteLineDto
{
    public Guid Id { get; set; }
    public Guid SalesOrderLineId { get; set; }
    public Guid? ProductId { get; set; }
    public decimal ShippedQuantity { get; set; }
}

public class GetDeliveryNoteQueryHandler : IRequestHandler<GetDeliveryNoteQuery, DeliveryNoteDetailDto?>
{
    private readonly ISalesDbContext _context;

    public GetDeliveryNoteQueryHandler(ISalesDbContext context) => _context = context;

    public async Task<DeliveryNoteDetailDto?> Handle(GetDeliveryNoteQuery request, CancellationToken cancellationToken)
    {
        var note = await _context.DeliveryNotes
            .Include(d => d.Lines)
            .FirstOrDefaultAsync(d => d.Id == request.Id, cancellationToken);

        if (note == null) return null;

        return new DeliveryNoteDetailDto
        {
            Id = note.Id,
            CompanyId = note.CompanyId,
            SalesOrderId = note.SalesOrderId,
            Number = note.Number,
            DeliveryDate = note.DeliveryDate,
            CreatedAt = note.CreatedAt,
            Lines = note.Lines.Select(l => new DeliveryNoteLineDto
            {
                Id = l.Id,
                SalesOrderLineId = l.SalesOrderLineId,
                ProductId = l.ProductId,
                ShippedQuantity = l.ShippedQuantity,
            }).ToList()
        };
    }
}

public record GetAllDeliveryNotesQuery(
    int Page = 1,
    int PageSize = 50,
    string? Search = null) : IRequest<PaginatedDeliveryNotesResult>;

public record PaginatedDeliveryNotesResult(
    List<DeliveryNoteSummaryDto> Items,
    int TotalCount,
    int Page,
    int PageSize);

public class DeliveryNoteSummaryDto
{
    public Guid Id { get; set; }
    public string Number { get; set; } = string.Empty;
    public DateTime DeliveryDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public int LineCount { get; set; }
}

public class GetAllDeliveryNotesQueryHandler : IRequestHandler<GetAllDeliveryNotesQuery, PaginatedDeliveryNotesResult>
{
    private readonly ISalesDbContext _context;

    public GetAllDeliveryNotesQueryHandler(ISalesDbContext context) => _context = context;

    public async Task<PaginatedDeliveryNotesResult> Handle(GetAllDeliveryNotesQuery request, CancellationToken cancellationToken)
    {
        var query = _context.DeliveryNotes.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var search = request.Search.ToLowerInvariant();
            query = query.Where(d => d.Number.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Include(d => d.Lines)
            .OrderByDescending(d => d.DeliveryDate)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(d => new DeliveryNoteSummaryDto
            {
                Id = d.Id,
                Number = d.Number,
                DeliveryDate = d.DeliveryDate,
                CreatedAt = d.CreatedAt,
                LineCount = d.Lines.Count,
            })
            .ToListAsync(cancellationToken);

        return new PaginatedDeliveryNotesResult(items, totalCount, request.Page, request.PageSize);
    }
}

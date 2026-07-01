using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Handlers;

// ── Queries ──
public record GetInvoicesByStatusQuery(string? Status, Guid CompanyId) : IRequest<List<InvoiceSummaryDto>>;

public record InvoiceSummaryDto(Guid Id, string Number, string Series, int FiscalYear,
    DateTime IssueDate, DateTime DueDate, string Status, bool IsLocked,
    decimal Subtotal, decimal TaxAmount, decimal Total,
    string? VerifactuHuella = null, string? VerifactuQrUrl = null);

public class GetInvoicesByStatusHandler : IRequestHandler<GetInvoicesByStatusQuery, List<InvoiceSummaryDto>>
{
    private readonly IBillingDbContext _ctx;
    public GetInvoicesByStatusHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<List<InvoiceSummaryDto>> Handle(GetInvoicesByStatusQuery request, CancellationToken ct)
    {
        var q = _ctx.Invoices.AsQueryable();
        if (!string.IsNullOrEmpty(request.Status))
            q = q.Where(i => i.Status == request.Status);

        return await q.OrderByDescending(i => i.IssueDate)
            .Select(i => new InvoiceSummaryDto(i.Id, i.Number, i.Series, i.FiscalYear,
                i.IssueDate, i.DueDate, i.Status, i.IsLocked, i.Subtotal, i.TaxAmount, i.Total,
                i.VerifactuHuella, i.VerifactuQrUrl))
            .ToListAsync(ct);
    }
}

// ── Commands ──
public record CreateInvoiceModuleCommand(
    Guid ClientId, string Series, int FiscalYear,
    DateTime IssueDate, DateTime DueDate,
    List<InvoiceLineDto> Lines) : IRequest<Guid>;

public record InvoiceLineDto(string Description, decimal Quantity, decimal UnitPrice,
    decimal TaxRate, Guid? ProductId = null);

public class CreateInvoiceModuleHandler : IRequestHandler<CreateInvoiceModuleCommand, Guid>
{
    private readonly IBillingDbContext _ctx;
    public CreateInvoiceModuleHandler(IBillingDbContext ctx) => _ctx = ctx;

    public async Task<Guid> Handle(CreateInvoiceModuleCommand request, CancellationToken ct)
    {
        // Get last sequence for this series/year
        var lastSeq = await _ctx.Invoices
            .Where(i => i.Series == request.Series && i.FiscalYear == request.FiscalYear)
            .MaxAsync(i => (int?)i.SequenceNumber, ct) ?? 0;

        var seq = lastSeq + 1;
        var number = $"{request.Series}-{request.FiscalYear}-{seq:D6}";

        var lines = request.Lines.Select(l => new InvoiceLine
        {
            Id = Guid.NewGuid(),
            Description = l.Description,
            Quantity = l.Quantity,
            UnitPrice = l.UnitPrice,
            TaxRate = l.TaxRate,
            TaxAmount = Math.Round(l.Quantity * l.UnitPrice * l.TaxRate / 100, 2),
            LineTotal = Math.Round(l.Quantity * l.UnitPrice, 2),
            ProductId = l.ProductId
        }).ToList();

        var subtotal = lines.Sum(l => l.LineTotal);
        var taxAmount = lines.Sum(l => l.TaxAmount);

        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            ClientId = request.ClientId,
            Series = request.Series,
            FiscalYear = request.FiscalYear,
            SequenceNumber = seq,
            Number = number,
            IssueDate = request.IssueDate,
            DueDate = request.DueDate,
            Status = "Draft",
            IsLocked = false,
            Subtotal = subtotal,
            TaxAmount = taxAmount,
            Total = subtotal + taxAmount,
            InvoiceLines = lines
        };

        _ctx.Invoices.Add(invoice);
        await _ctx.SaveChangesAsync(ct);
        return invoice.Id;
    }
}

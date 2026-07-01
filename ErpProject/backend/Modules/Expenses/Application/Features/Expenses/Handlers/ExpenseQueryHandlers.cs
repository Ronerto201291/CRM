using Erp.Application.DTOs;
using Erp.Modules.Expenses.Application.Features.Expenses.Queries;
using Erp.Modules.Expenses.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Expenses.Application.Features.Expenses.Handlers;

public class GetExpenseUploadsHandler : IRequestHandler<GetExpenseUploadsQuery, List<ExpenseUploadDto>>
{
    private readonly IExpensesDbContext _ctx;
    public GetExpenseUploadsHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<List<ExpenseUploadDto>> Handle(GetExpenseUploadsQuery request, CancellationToken ct)
    {
        return await _ctx.ExpenseUploads
            .OrderByDescending(u => u.UploadedAt)
            .Select(u => new ExpenseUploadDto
            {
                Id = u.Id, FileName = u.FileName, Status = u.Status,
                Comment = u.Comment, UploadedAt = u.UploadedAt, ExpenseDocumentId = u.ExpenseDocumentId
            })
            .ToListAsync(ct);
    }
}

public class GetExpenseDocumentsHandler : IRequestHandler<GetExpenseDocumentsQuery, List<ExpenseDocumentListDto>>
{
    private readonly IExpensesDbContext _ctx;
    public GetExpenseDocumentsHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<List<ExpenseDocumentListDto>> Handle(GetExpenseDocumentsQuery request, CancellationToken ct)
    {
        return await _ctx.ExpenseDocuments
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new ExpenseDocumentListDto
            {
                Id = e.Id, InvoiceNumber = e.InvoiceNumber, SupplierName = e.SupplierName,
                SupplierTaxId = e.SupplierTaxId, IssueDate = e.IssueDate,
                TaxBase = e.TaxBase, VATRate = e.VATRate, VATAmount = e.VATAmount,
                IRPFRate = e.IRPFRate, IRPFAmount = e.IRPFAmount, Total = e.Total,
                Status = e.Status, IsValidated = e.IsValidated, ValidatedAt = e.ValidatedAt,
                CreatedAt = e.CreatedAt, OcrConfidence = e.OcrConfidence,
                LineCount = e.Lines.Count
            })
            .ToListAsync(ct);
    }
}

public class GetExpenseByIdHandler : IRequestHandler<GetExpenseByIdQuery, ExpenseDocumentDetailDto?>
{
    private readonly IExpensesDbContext _ctx;
    public GetExpenseByIdHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<ExpenseDocumentDetailDto?> Handle(GetExpenseByIdQuery request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments
            .Include(e => e.Lines.OrderBy(l => l.SortOrder))
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);

        if (doc == null) return null;

        return new ExpenseDocumentDetailDto
        {
            Id = doc.Id, InvoiceNumber = doc.InvoiceNumber, SupplierName = doc.SupplierName,
            SupplierTaxId = doc.SupplierTaxId, SupplierId = doc.SupplierId,
            IssueDate = doc.IssueDate, TaxBase = doc.TaxBase, VATRate = doc.VATRate,
            VATAmount = doc.VATAmount, IRPFRate = doc.IRPFRate, IRPFAmount = doc.IRPFAmount,
            Total = doc.Total, Status = doc.Status, IsValidated = doc.IsValidated,
            ValidatedAt = doc.ValidatedAt, IsLocked = doc.IsLocked,
            HashSignature = doc.HashSignature, OcrConfidence = doc.OcrConfidence,
            OcrRawData = doc.OcrRawData, CreatedAt = doc.CreatedAt, UpdatedAt = doc.UpdatedAt,
            Lines = doc.Lines.Select(l => new ExpenseLineDetailDto
            {
                Id = l.Id, SortOrder = l.SortOrder, Description = l.Description,
                Quantity = l.Quantity, UnitPrice = l.UnitPrice, VATRate = l.VATRate,
                LineTotal = l.LineTotal, ProductId = l.ProductId
            }).ToList()
        };
    }
}

public class GetSupplierExpensesHandler : IRequestHandler<GetSupplierExpensesQuery, List<SupplierExpenseDto>>
{
    private readonly IExpensesDbContext _ctx;
    public GetSupplierExpensesHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<List<SupplierExpenseDto>> Handle(GetSupplierExpensesQuery request, CancellationToken ct)
    {
        return await _ctx.ExpenseDocuments
            .Where(e => e.SupplierId == request.SupplierId)
            .OrderByDescending(e => e.CreatedAt)
            .Select(e => new SupplierExpenseDto
            {
                Id = e.Id, InvoiceNumber = e.InvoiceNumber,
                Total = e.Total, Status = e.Status, IssueDate = e.IssueDate
            })
            .ToListAsync(ct);
    }
}

public class GetExpenseStatsHandler : IRequestHandler<GetExpenseStatsQuery, ExpenseStatsDto>
{
    private readonly IExpensesDbContext _ctx;
    public GetExpenseStatsHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<ExpenseStatsDto> Handle(GetExpenseStatsQuery request, CancellationToken ct)
    {
        var docs = _ctx.ExpenseDocuments;
        return new ExpenseStatsDto
        {
            Pending           = await docs.CountAsync(d => d.Status == "Draft" || d.Status == "Reviewed", ct),
            Approved          = await docs.CountAsync(d => d.Status == "Approved", ct),
            TotalVATSoportado = await docs.Where(d => d.Status == "Approved").SumAsync(d => d.VATAmount ?? 0, ct),
            TotalBase         = await docs.Where(d => d.Status == "Approved").SumAsync(d => d.TaxBase  ?? 0, ct)
        };
    }
}

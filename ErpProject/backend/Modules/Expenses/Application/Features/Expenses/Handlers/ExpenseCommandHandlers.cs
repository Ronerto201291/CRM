using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Audit;
using Erp.Modules.Expenses.Domain.Entities;
using Erp.Modules.Expenses.Application.Features.Expenses.Commands;
using Erp.Modules.Expenses.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace Erp.Modules.Expenses.Application.Features.Expenses.Handlers;

public class CreateExpenseDocumentHandler : IRequestHandler<CreateExpenseDocumentCommand, Guid>
{
    private readonly IExpensesDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateExpenseDocumentHandler(IExpensesDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateExpenseDocumentCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no identificado.");

        var doc = new ExpenseDocument
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceNumber = request.InvoiceNumber,
            SupplierName = request.SupplierName,
            SupplierTaxId = request.SupplierTaxId,
            IssueDate = request.IssueDate ?? DateTime.UtcNow,
            TaxBase = request.TaxBase,
            VATRate = request.VATRate,
            VATAmount = request.VATAmount,
            IRPFRate = request.IRPFRate,
            IRPFAmount = request.IRPFAmount,
            Total = request.Total,
            Status = "Draft",
            OcrConfidence = null // null = entrada manual (sin OCR)
        };

        _ctx.ExpenseDocuments.Add(doc);
        await _ctx.SaveChangesAsync(ct);
        return doc.Id;
    }
}

public class UpdateExpenseDraftHandler : IRequestHandler<UpdateExpenseDraftCommand, bool>
{
    private readonly IExpensesDbContext _ctx;
    public UpdateExpenseDraftHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateExpenseDraftCommand request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (doc == null) return false;
        if (doc.IsLocked) throw new InvalidOperationException("Documento bloqueado.");

        doc.InvoiceNumber = request.InvoiceNumber; doc.SupplierName  = request.SupplierName;
        doc.SupplierTaxId = request.SupplierTaxId; doc.IssueDate     = request.IssueDate;
        doc.TaxBase       = request.TaxBase;       doc.VATRate       = request.VATRate;
        doc.VATAmount     = request.VATAmount;     doc.IRPFRate      = request.IRPFRate;
        doc.IRPFAmount    = request.IRPFAmount;    doc.Total         = request.Total;
        doc.Status = "Reviewed";

        if (request.Lines != null)
        {
            _ctx.ExpenseDocumentLines.RemoveRange(doc.Lines);
            var newLines = request.Lines.Select((l, i) => new ExpenseDocumentLine
            {
                Id = Guid.NewGuid(), ExpenseDocumentId = doc.Id,
                Description = l.Description, Quantity = l.Quantity, UnitPrice = l.UnitPrice,
                VATRate = l.VATRate, LineTotal = l.LineTotal, ProductId = l.ProductId,
                SortOrder = i + 1
            }).ToList();
            _ctx.ExpenseDocumentLines.AddRange(newLines);
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class ApproveExpenseHandler : IRequestHandler<ApproveExpenseCommand, ApproveExpenseResult>
{
    private readonly IExpensesDbContext _ctx;
    private readonly IApplicationDbContext _app;
    private readonly IPublisher _publisher;
    private readonly IHttpContextCurrentUserAccessor _currentUser;

    public ApproveExpenseHandler(
        IExpensesDbContext ctx,
        IApplicationDbContext app,
        IPublisher publisher,
        IHttpContextCurrentUserAccessor currentUser)
    {
        _ctx = ctx; _app = app; _publisher = publisher; _currentUser = currentUser;
    }

    public async Task<ApproveExpenseResult> Handle(ApproveExpenseCommand request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments
            .Include(e => e.Lines)
            .FirstOrDefaultAsync(e => e.Id == request.Id, ct);
        if (doc == null) throw new KeyNotFoundException("Documento no encontrado.");
        if (doc.IsLocked) throw new InvalidOperationException("Documento ya aprobado y bloqueado.");

        doc.Status = "Approved"; doc.IsValidated = true;
        doc.ValidatedAt = DateTime.UtcNow; doc.IsLocked = true;

        var hashInput = string.Join("|", doc.Id, doc.InvoiceNumber, doc.Total,
            doc.IssueDate?.ToString("yyyy-MM-dd"), doc.SupplierTaxId);
        doc.HashSignature = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(hashInput)));

        await _ctx.SaveChangesAsync(ct);

        if (_currentUser.UserId is Guid auditUserId)
        {
            var auditEntry = new AuditLog
            {
                Id = Guid.NewGuid(), CompanyId = doc.CompanyId,
                UserId = auditUserId, Entity = "ExpenseDocument", EntityId = doc.Id,
                Action = "Approved", Timestamp = DateTime.UtcNow,
                OldValues = System.Text.Json.JsonSerializer.Serialize(new { Status = "Draft" }),
                NewValues = System.Text.Json.JsonSerializer.Serialize(new { doc.Status, doc.HashSignature })
            };
            var auditHashInput = string.Join("|", auditEntry.Entity, auditEntry.EntityId,
                auditEntry.Action, auditEntry.Timestamp.ToString("O"));
            auditEntry.Hash = Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(auditHashInput)));
            _app.AuditLogs.Add(auditEntry);
            await _app.SaveChangesAsync(ct);
        }

        await _publisher.Publish(new ExpenseApprovedEvent
        {
            ExpenseDocumentId = doc.Id, CompanyId = doc.CompanyId,
            SupplierId = doc.SupplierId, SupplierName = doc.SupplierName, TaxBase = doc.TaxBase ?? 0,
            VATAmount = doc.VATAmount ?? 0, IRPFAmount = doc.IRPFAmount,
            Total = doc.Total ?? 0, IssueDate = doc.IssueDate ?? DateTime.UtcNow,
            Lines = doc.Lines
                .Where(l => l.ProductId.HasValue && l.Quantity > 0)
                .Select(l => new ExpenseLineEventDto
                {
                    ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitPrice
                }).ToList()
        }, ct);

        return new ApproveExpenseResult
        {
            Message = "Gasto aprobado. Asiento contable generado.",
            HashSignature = doc.HashSignature!,
            LinesWithInventory = doc.Lines.Count(l => l.ProductId.HasValue)
        };
    }
}

public class AddExpenseLineHandler : IRequestHandler<AddExpenseLineCommand, Guid>
{
    private readonly IExpensesDbContext _ctx;
    public AddExpenseLineHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<Guid> Handle(AddExpenseLineCommand request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments.FirstOrDefaultAsync(e => e.Id == request.ExpenseDocumentId, ct)
            ?? throw new KeyNotFoundException("Documento no encontrado.");
        if (doc.IsLocked) throw new InvalidOperationException("Documento bloqueado.");

        var maxOrder = await _ctx.ExpenseDocumentLines
            .Where(l => l.ExpenseDocumentId == request.ExpenseDocumentId)
            .MaxAsync(l => (int?)l.SortOrder, ct) ?? 0;

        var line = new ExpenseDocumentLine
        {
            Id = Guid.NewGuid(), ExpenseDocumentId = request.ExpenseDocumentId,
            Description = request.Description, Quantity = request.Quantity,
            UnitPrice = request.UnitPrice, VATRate = request.VATRate,
            LineTotal = request.LineTotal, ProductId = request.ProductId,
            SortOrder = maxOrder + 1
        };
        _ctx.ExpenseDocumentLines.Add(line);
        await _ctx.SaveChangesAsync(ct);
        return line.Id;
    }
}

public class UpdateExpenseLineHandler : IRequestHandler<UpdateExpenseLineCommand, bool>
{
    private readonly IExpensesDbContext _ctx;
    public UpdateExpenseLineHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateExpenseLineCommand request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments.FirstOrDefaultAsync(e => e.Id == request.ExpenseDocumentId, ct);
        if (doc == null) return false;
        if (doc.IsLocked) throw new InvalidOperationException("Documento bloqueado.");

        var line = await _ctx.ExpenseDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.LineId && l.ExpenseDocumentId == request.ExpenseDocumentId, ct);
        if (line == null) return false;

        line.Description = request.Description; line.Quantity  = request.Quantity;
        line.UnitPrice   = request.UnitPrice;   line.VATRate   = request.VATRate;
        line.LineTotal   = request.LineTotal;   line.ProductId = request.ProductId;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class DeleteExpenseLineHandler : IRequestHandler<DeleteExpenseLineCommand, bool>
{
    private readonly IExpensesDbContext _ctx;
    public DeleteExpenseLineHandler(IExpensesDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(DeleteExpenseLineCommand request, CancellationToken ct)
    {
        var doc = await _ctx.ExpenseDocuments.FirstOrDefaultAsync(e => e.Id == request.ExpenseDocumentId, ct);
        if (doc == null) return false;
        if (doc.IsLocked) throw new InvalidOperationException("Documento bloqueado.");

        var line = await _ctx.ExpenseDocumentLines
            .FirstOrDefaultAsync(l => l.Id == request.LineId && l.ExpenseDocumentId == request.ExpenseDocumentId, ct);
        if (line == null) return false;

        _ctx.ExpenseDocumentLines.Remove(line);
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Application.Commands;

/// <summary>
/// Crea una Factura Rectificativa (Abono) para anular una Factura existente.
/// Conforme a RD 1619/2012: Las rectificativas deben emitirse en lugar de borrar.
/// </summary>
public class CreateCreditNoteCommand : IRequest<CreateCreditNoteResponse>
{
    public Guid InvoiceId { get; set; }
    public string Reason { get; set; } = string.Empty;
}

public class CreateCreditNoteResponse
{
    public Guid CreditNoteId { get; set; }
    public string Number { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class CreateCreditNoteHandler : IRequestHandler<CreateCreditNoteCommand, CreateCreditNoteResponse>
{
    private readonly IBillingDbContext _context;
    private readonly ITenantContext _tenantContext;

    public CreateCreditNoteHandler(IBillingDbContext context, ITenantContext tenantContext)
    {
        _context = context;
        _tenantContext = tenantContext;
    }

    public async Task<CreateCreditNoteResponse> Handle(
        CreateCreditNoteCommand request,
        CancellationToken cancellationToken)
    {
        var tenantId = _tenantContext.TenantId;
        if (!tenantId.HasValue)
            throw new InvalidOperationException("Tenant no encontrado");

        // 1. Obtener la factura original
        var originalInvoice = await _context.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.Id == request.InvoiceId && i.CompanyId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (originalInvoice == null)
            throw new InvalidOperationException("Factura no encontrada");

        if (originalInvoice.Status == "Draft")
            throw new InvalidOperationException("No se puede crear rectificativa de una factura en borrador");

        // 2. Calcular siguiente número de secuencia para serie R (rectificativa)
        var rectSeries = "R";
        var fiscalYear = DateTime.UtcNow.Year;
        var nextSeq = await _context.Invoices
            .Where(i => i.Series == rectSeries && i.FiscalYear == fiscalYear && i.CompanyId == tenantId.Value)
            .MaxAsync(i => (int?)i.SequenceNumber, cancellationToken) ?? 0;
        nextSeq++;
        var number = $"{rectSeries}-{fiscalYear}-{nextSeq:D6}";

        // 3. Crear nueva Factura Rectificativa con líneas invertidas
        var creditNote = new Invoice
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId.Value,
            ClientId = originalInvoice.ClientId,
            Series = rectSeries,
            FiscalYear = fiscalYear,
            SequenceNumber = nextSeq,
            Number = number,
            InvoiceType = "Rectificativa",
            RectifiedInvoiceId = originalInvoice.Id,
            IssueDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30),
            Status = "Draft",
        };

        foreach (var line in originalInvoice.InvoiceLines)
        {
            creditNote.InvoiceLines.Add(new InvoiceLine
            {
                Id = Guid.NewGuid(),
                InvoiceId = creditNote.Id,
                Description = $"[Rectificativa] {line.Description}",
                Quantity = -line.Quantity,
                UnitPrice = line.UnitPrice,
                TaxRate = line.TaxRate,
                SurchargeRate = line.SurchargeRate,
                LineTotal = -line.LineTotal,
            });
        }

        _context.Invoices.Add(creditNote);
        await _context.SaveChangesAsync(cancellationToken);

        return new CreateCreditNoteResponse
        {
            CreditNoteId = creditNote.Id,
            Number = creditNote.Number,
            TotalAmount = creditNote.InvoiceLines.Sum(il => il.LineTotal),
            Message = $"Factura Rectificativa #{creditNote.Number} creada. Rectifica factura #{originalInvoice.Number}"
        };
    }
}

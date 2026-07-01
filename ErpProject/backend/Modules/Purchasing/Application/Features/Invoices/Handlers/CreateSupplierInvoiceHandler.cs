using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Application.Validators;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Application.Common.Interfaces;
using MediatR;

namespace Erp.Modules.Purchasing.Application.Features.Invoices.Handlers;

public class CreateSupplierInvoiceHandler : IRequestHandler<CreateSupplierInvoiceCommand, Guid>
{
    private readonly IPurchasingDbContext _context;
    private readonly IApplicationDbContext _appContext;
    private readonly Erp.Application.Common.Interfaces.ITenantContext _tenant;

    public CreateSupplierInvoiceHandler(IPurchasingDbContext context, IApplicationDbContext appContext, Erp.Application.Common.Interfaces.ITenantContext tenant)
    {
        _context = context;
        _appContext = appContext;
        _tenant = tenant;
    }

    public async Task<Guid> Handle(CreateSupplierInvoiceCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FindAsync(new object[] { request.PurchaseOrderId }, cancellationToken);
        if (po == null) throw new InvalidOperationException("Purchase order not found");

        // Determine tenant/company tolerance: prefer company's setting if available, otherwise 0
        var tenantId = _tenant.TenantId ?? po.CompanyId;
        var company = await _appContext.Companies.FindAsync(new object[] { tenantId }, cancellationToken);
        var tolerance = company?.MatchingToleranceAmount ?? 0m;

        var invoice = new SupplierInvoice
        {
            CompanyId = po.CompanyId,
            PurchaseOrderId = request.PurchaseOrderId,
            Number = request.Number,
            InvoiceDate = request.InvoiceDate,
            TotalAmount = request.TotalAmount
        };

        foreach (var l in request.Lines)
        {
            var line = new SupplierInvoiceLine
            {
                SupplierInvoiceId = invoice.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                Quantity = l.Quantity,
                UnitPrice = l.UnitPrice
            };
            invoice.Lines.Add(line);
            _context.SupplierInvoiceLines.Add(line);
        }

        // Validate 3-way match using tenant/company configured tolerance
        ThreeWayMatchValidator.ValidateInvoiceAgainstOrderAndReceipt(invoice, _context, tolerance);

        _context.SupplierInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        return invoice.Id;
    }
}

using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Application.Validators;
using Erp.Modules.Purchasing.Domain.Entities;
using FluentValidation.Results;
using MediatR;
using ValidationException = FluentValidation.ValidationException;

namespace Erp.Modules.Purchasing.Application.Features.Invoices.Handlers;

public class CreateSupplierInvoiceHandler : IRequestHandler<CreateSupplierInvoiceCommand, Guid>
{
    private readonly IPurchasingDbContext _context;
    private readonly IApplicationDbContext _appContext;
    private readonly ITenantContext _tenant;
    private readonly ISupplierInfoService _supplierInfo;
    private readonly IPublisher _publisher;

    public CreateSupplierInvoiceHandler(
        IPurchasingDbContext context,
        IApplicationDbContext appContext,
        ITenantContext tenant,
        ISupplierInfoService supplierInfo,
        IPublisher publisher)
    {
        _context = context;
        _appContext = appContext;
        _tenant = tenant;
        _supplierInfo = supplierInfo;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(CreateSupplierInvoiceCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FindAsync(new object[] { request.PurchaseOrderId }, cancellationToken);
        if (po == null) throw new InvalidOperationException("Purchase order not found");
        if (po.Status != PurchaseOrderStatuses.Approved)
            throw new InvalidOperationException("El pedido debe estar aprobado antes de facturar.");

        var supplier = await _supplierInfo.GetByIdAsync(request.SupplierId, cancellationToken);
        if (supplier is null)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.SupplierId),
                    "El proveedor no existe en CRM o no pertenece a esta empresa.")
            });
        }

        if (po.SupplierId is Guid poSupplierId && poSupplierId != request.SupplierId)
        {
            throw new ValidationException(new[]
            {
                new ValidationFailure(nameof(request.SupplierId),
                    "El proveedor debe coincidir con el del pedido de compra.")
            });
        }

        var tenantId = _tenant.TenantId ?? po.CompanyId;
        var company = await _appContext.Companies.FindAsync(new object[] { tenantId }, cancellationToken);
        var tolerance = company?.MatchingToleranceAmount ?? 0m;

        var invoice = new SupplierInvoice
        {
            CompanyId = po.CompanyId,
            PurchaseOrderId = request.PurchaseOrderId,
            SupplierId = request.SupplierId,
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

        ThreeWayMatchValidator.ValidateInvoiceAgainstOrderAndReceipt(invoice, _context, tolerance);

        _context.SupplierInvoices.Add(invoice);
        await _context.SaveChangesAsync(cancellationToken);

        var taxBase = invoice.Lines.Sum(l => l.Quantity * l.UnitPrice);
        var total = request.TotalAmount;
        var vatAmount = total > taxBase ? total - taxBase : 0m;
        if (vatAmount <= 0m && total > 0m && taxBase <= 0m)
            taxBase = total;

        await _publisher.Publish(new SupplierInvoiceCreatedEvent
        {
            SupplierInvoiceId = invoice.Id,
            CompanyId = invoice.CompanyId,
            PurchaseOrderId = invoice.PurchaseOrderId,
            InvoiceNumber = invoice.Number,
            TaxBase = taxBase,
            VATAmount = vatAmount,
            Total = total,
            InvoiceDate = invoice.InvoiceDate,
        }, cancellationToken);

        return invoice.Id;
    }
}

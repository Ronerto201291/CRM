using Erp.Application.Common.Events;
using Erp.Modules.Purchasing.Application.Features.Receipts.Commands;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Application.Features.Receipts.Handlers;

public class CreateGoodsReceiptHandler : IRequestHandler<CreateGoodsReceiptCommand, Guid>
{
    private readonly IPurchasingDbContext _context;
    private readonly IPublisher _publisher;

    public CreateGoodsReceiptHandler(IPurchasingDbContext context, IPublisher publisher)
    {
        _context = context;
        _publisher = publisher;
    }

    public async Task<Guid> Handle(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FindAsync(new object[] { request.PurchaseOrderId }, cancellationToken);
        if (po == null)
            throw new InvalidOperationException("Purchase order not found");
        if (po.Status != PurchaseOrderStatuses.Approved)
            throw new InvalidOperationException("El pedido debe estar aprobado antes de registrar recepciones.");

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            CompanyId = po.CompanyId,
            PurchaseOrderId = request.PurchaseOrderId,
            Number = request.Number,
            ReceiptDate = request.ReceiptDate
        };

        var lineIds = request.Lines.Select(l => l.PurchaseOrderLineId).ToList();
        var purchaseOrderLines = await _context.PurchaseOrderLines
            .Where(pol => lineIds.Contains(pol.Id))
            .ToDictionaryAsync(pol => pol.Id, cancellationToken);

        foreach (var l in request.Lines)
        {
            if (!purchaseOrderLines.TryGetValue(l.PurchaseOrderLineId, out var pol))
                throw new InvalidOperationException("Purchase order line not found");
            if (pol.PurchaseOrderId != request.PurchaseOrderId)
                throw new InvalidOperationException("Purchase order line does not belong to this order");
            if (l.QuantityReceived > pol.Quantity)
                throw new InvalidOperationException("Received quantity exceeds ordered quantity");

            receipt.Lines.Add(new GoodsReceiptLine
            {
                Id = Guid.NewGuid(),
                GoodsReceiptId = receipt.Id,
                PurchaseOrderLineId = l.PurchaseOrderLineId,
                ProductId = l.ProductId,
                QuantityReceived = l.QuantityReceived,
                UnitPrice = l.UnitPrice
            });
        }

        _context.GoodsReceipts.Add(receipt);
        await _context.SaveChangesAsync(cancellationToken);

        await _publisher.Publish(new GoodsReceiptCreatedEvent
        {
            GoodsReceiptId = receipt.Id,
            CompanyId = receipt.CompanyId,
            ReceiptNumber = receipt.Number,
            Lines = receipt.Lines
                .Where(l => l.ProductId.HasValue && l.QuantityReceived > 0)
                .Select(l => new StockLineEventDto
                {
                    ProductId = l.ProductId,
                    Quantity = l.QuantityReceived,
                    UnitCost = l.UnitPrice
                })
                .ToList()
        }, cancellationToken);

        return receipt.Id;
    }
}

using Erp.Modules.Purchasing.Application.Features.Receipts.Commands;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using MediatR;

namespace Erp.Modules.Purchasing.Application.Features.Receipts.Handlers;

public class CreateGoodsReceiptHandler : IRequestHandler<CreateGoodsReceiptCommand, Guid>
{
    private readonly IPurchasingDbContext _context;

    public CreateGoodsReceiptHandler(IPurchasingDbContext context)
    {
        _context = context;
    }

    public async Task<Guid> Handle(CreateGoodsReceiptCommand request, CancellationToken cancellationToken)
    {
        var po = await _context.PurchaseOrders.FindAsync(new object[] { request.PurchaseOrderId }, cancellationToken);
        if (po == null)
            throw new InvalidOperationException("Purchase order not found");

        var receipt = new GoodsReceipt
        {
            Id = Guid.NewGuid(),
            CompanyId = po.CompanyId,
            PurchaseOrderId = request.PurchaseOrderId,
            Number = request.Number,
            ReceiptDate = request.ReceiptDate
        };

        foreach (var l in request.Lines)
        {
            var pol = await _context.PurchaseOrderLines.FindAsync(new object[] { l.PurchaseOrderLineId }, cancellationToken);
            if (pol == null) throw new InvalidOperationException("Purchase order line not found");
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
        return receipt.Id;
    }
}

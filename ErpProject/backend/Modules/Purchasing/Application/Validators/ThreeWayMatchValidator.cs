using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;

namespace Erp.Modules.Purchasing.Application.Validators;

public static class ThreeWayMatchValidator
{
    public static void ValidateInvoiceAgainstOrderAndReceipt(SupplierInvoice invoice, IPurchasingDbContext dbContext, decimal toleranceAmount = 0m)
    {
        if (invoice == null) throw new ArgumentNullException(nameof(invoice));

        foreach (var il in invoice.Lines)
        {
            var pol = dbContext.PurchaseOrderLines.FirstOrDefault(p => p.Id == il.PurchaseOrderLineId);
            if (pol == null) throw new InvalidOperationException("Purchase order line not found for invoice line");

            var receipts = dbContext.GoodsReceiptLines.Where(g => g.PurchaseOrderLineId == pol.Id).ToList();
            var receivedQty = receipts.Sum(r => r.QuantityReceived);

            if (il.Quantity > receivedQty)
                throw new InvalidOperationException("Invoice quantity exceeds received quantity");

            var invoiceLineAmount = il.Quantity * il.UnitPrice;
            var orderLineAmount = pol.Quantity * pol.UnitPrice;

            if (Math.Abs(invoiceLineAmount - orderLineAmount) > toleranceAmount)
                throw new InvalidOperationException("Invoice line amount differs from order beyond tolerance");
        }
    }
}

namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Resolves whether a Billing invoice originated from a Sales customer invoice
/// (order-to-cash path where stock is decremented on delivery, not on fiscal lock).
/// </summary>
public interface IBillingInvoiceSalesLinkQuery
{
    Task<Guid?> GetSalesOrderIdForBillingInvoiceAsync(Guid billingInvoiceId, CancellationToken cancellationToken = default);
}

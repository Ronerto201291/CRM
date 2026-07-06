using Erp.Application.Common.Interfaces;

namespace Erp.Tests.TestSupport;

public sealed class FakeBillingInvoiceSalesLinkQuery(Guid? salesOrderId = null) : IBillingInvoiceSalesLinkQuery
{
    public Task<Guid?> GetSalesOrderIdForBillingInvoiceAsync(Guid billingInvoiceId, CancellationToken cancellationToken = default)
        => Task.FromResult(salesOrderId);
}

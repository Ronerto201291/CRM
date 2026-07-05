using Erp.Application.Common.Interfaces;
using Erp.Modules.Sales.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Infrastructure.Services;

public sealed class BillingInvoiceSalesLinkQuery : IBillingInvoiceSalesLinkQuery
{
    private readonly ISalesDbContext _context;

    public BillingInvoiceSalesLinkQuery(ISalesDbContext context) => _context = context;

    public async Task<Guid?> GetSalesOrderIdForBillingInvoiceAsync(Guid billingInvoiceId, CancellationToken cancellationToken = default)
    {
        return await _context.CustomerInvoices
            .AsNoTracking()
            .Where(ci => ci.BillingInvoiceId == billingInvoiceId)
            .Select(ci => (Guid?)ci.SalesOrderId)
            .FirstOrDefaultAsync(cancellationToken);
    }
}

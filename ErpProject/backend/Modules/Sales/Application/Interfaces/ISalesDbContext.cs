using Erp.Modules.Sales.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Sales.Application.Interfaces
{
    public interface ISalesDbContext
    {
        DbSet<SalesOrder> SalesOrders { get; }
        DbSet<SalesOrderLine> SalesOrderLines { get; }
        DbSet<DeliveryNote> DeliveryNotes { get; }
        DbSet<DeliveryNoteLine> DeliveryNoteLines { get; }
        DbSet<CustomerInvoice> CustomerInvoices { get; }
        DbSet<CustomerInvoiceLine> CustomerInvoiceLines { get; }
        Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    }
}

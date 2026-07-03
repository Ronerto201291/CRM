using Erp.Modules.Sales.Application.Features.Invoices.Queries;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class GetCustomerInvoiceQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsInvoiceWithLines()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-invoice-detail-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var invoiceId = Guid.NewGuid();
        var invoice = new CustomerInvoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            SalesOrderId = Guid.NewGuid(),
            Number = "FV-DET-1",
            InvoiceDate = DateTime.UtcNow,
            SubTotal = 200m,
            TaxAmount = 42m,
            Total = 242m,
        };
        invoice.Lines.Add(new CustomerInvoiceLine
        {
            Id = Guid.NewGuid(),
            CustomerInvoiceId = invoiceId,
            SalesOrderLineId = Guid.NewGuid(),
            BilledQuantity = 2m,
            UnitPrice = 100m,
        });
        ctx.CustomerInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var handler = new GetCustomerInvoiceQueryHandler(ctx);
        var result = await handler.Handle(new GetCustomerInvoiceQuery(invoiceId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("FV-DET-1", result!.Number);
        Assert.Single(result.Lines);
        Assert.Equal(200m, result.Lines[0].LineTotal);
    }

    [Fact]
    public async Task Handle_UnknownId_ReturnsNull()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-invoice-missing-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var handler = new GetCustomerInvoiceQueryHandler(ctx);
        var result = await handler.Handle(new GetCustomerInvoiceQuery(Guid.NewGuid()), CancellationToken.None);
        Assert.Null(result);
    }
}

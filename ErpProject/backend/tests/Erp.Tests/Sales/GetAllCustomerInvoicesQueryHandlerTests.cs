using Erp.Modules.Sales.Application.Features.Invoices.Queries;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class GetAllCustomerInvoicesQueryHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsPaginatedInvoicesForTenant()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-invoices-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        var invoiceId = Guid.NewGuid();
        var invoice = new CustomerInvoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            SalesOrderId = Guid.NewGuid(),
            Number = "FV-001",
            InvoiceDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            SubTotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
        };
        invoice.Lines.Add(new CustomerInvoiceLine
        {
            Id = Guid.NewGuid(),
            CustomerInvoiceId = invoiceId,
            SalesOrderLineId = Guid.NewGuid(),
            BilledQuantity = 1m,
            UnitPrice = 100m,
        });
        ctx.CustomerInvoices.Add(invoice);
        await ctx.SaveChangesAsync();

        var handler = new GetAllCustomerInvoicesQueryHandler(ctx);
        var result = await handler.Handle(new GetAllCustomerInvoicesQuery(1, 50), CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Single(result.Items);
        Assert.Equal("FV-001", result.Items[0].Number);
        Assert.Equal(1, result.Items[0].LineCount);
    }

    [Fact]
    public async Task Handle_SearchFiltersByNumber()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-invoices-search-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.CustomerInvoices.AddRange(
            new CustomerInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SalesOrderId = Guid.NewGuid(),
                Number = "FV-ALPHA",
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 10,
                TaxAmount = 2,
                Total = 12,
            },
            new CustomerInvoice
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                SalesOrderId = Guid.NewGuid(),
                Number = "FV-BETA",
                InvoiceDate = DateTime.UtcNow,
                SubTotal = 20,
                TaxAmount = 4,
                Total = 24,
            });
        await ctx.SaveChangesAsync();

        var handler = new GetAllCustomerInvoicesQueryHandler(ctx);
        var result = await handler.Handle(new GetAllCustomerInvoicesQuery(1, 50, "alpha"), CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal("FV-ALPHA", result.Items[0].Number);
    }
}

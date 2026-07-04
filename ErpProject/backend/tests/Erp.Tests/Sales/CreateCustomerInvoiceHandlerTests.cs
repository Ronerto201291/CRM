using Erp.Application.DTOs;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Sales.Application.Features.Invoices.Commands;
using Erp.Modules.Sales.Application.Features.Invoices.Handlers;
using Erp.Modules.Sales.Domain.Entities;
using Erp.Modules.Sales.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Sales;

public class CreateCustomerInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_CreatesCustomerInvoice_AndUpdatesBilledQuantity()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-cust-inv-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.SalesOrders.Add(new SalesOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PED-500",
            OrderDate = DateTime.UtcNow,
            ClientId = Guid.NewGuid(),
            ClientName = "Cliente pedido",
            Lines =
            [
                new SalesOrderLine
                {
                    Id = lineId,
                    SalesOrderId = orderId,
                    Quantity = 5m,
                    UnitPrice = 40m,
                    DeliveredQuantity = 5m,
                    BilledQuantity = 0m,
                },
            ],
        });
        await ctx.SaveChangesAsync();

        var billingInvoiceId = Guid.NewGuid();
        var mediator = new ConfigurableFakeMediator();
        mediator.Register<CreateInvoiceCommand, InvoiceDto>(_ => new InvoiceDto
        {
            Id = billingInvoiceId,
            Number = "V-2026-000001",
            Subtotal = 200m,
            TaxAmount = 42m,
            SurchargeAmount = 0m,
            Total = 242m,
            Lines =
            [
                new InvoiceLineDto { UnitPrice = 40m, Quantity = 5m, TaxRate = 21m, LineTotal = 200m },
            ],
        });

        var handler = new CreateCustomerInvoiceHandler(ctx, mediator);
        var result = await handler.Handle(new CreateCustomerInvoiceCommand
        {
            SalesOrderId = orderId,
            Number = "FV-500",
            InvoiceDate = new DateTime(2026, 7, 1),
            Lines =
            [
                new CreateCustomerInvoiceLineDto
                {
                    SalesOrderLineId = lineId,
                    BilledQuantity = 5m,
                    UnitPrice = 40m,
                    TaxRate = 21m,
                },
            ],
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.Id);
        Assert.Equal(billingInvoiceId, result.BillingInvoiceId);
        Assert.Equal("V-2026-000001", result.BillingInvoiceNumber);

        var orderLine = await ctx.SalesOrderLines.SingleAsync(l => l.Id == lineId);
        Assert.Equal(5m, orderLine.BilledQuantity);

        var customerInvoice = await ctx.CustomerInvoices.Include(i => i.Lines).SingleAsync();
        Assert.Equal("FV-500", customerInvoice.Number);
        Assert.Equal(242m, customerInvoice.Total);
    }

    [Fact]
    public async Task Handle_Throws_WhenBillingMoreThanDelivered()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase($"sales-cust-inv-over-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new SalesDbContext(options, tenant);
        ctx.SalesOrders.Add(new SalesOrder
        {
            Id = orderId,
            CompanyId = companyId,
            Number = "PED-501",
            OrderDate = DateTime.UtcNow,
            ClientName = "Cliente",
            Lines =
            [
                new SalesOrderLine
                {
                    Id = lineId,
                    SalesOrderId = orderId,
                    Quantity = 10m,
                    UnitPrice = 10m,
                    DeliveredQuantity = 3m,
                },
            ],
        });
        await ctx.SaveChangesAsync();

        var handler = new CreateCustomerInvoiceHandler(ctx, new ConfigurableFakeMediator());
        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(
            new CreateCustomerInvoiceCommand
            {
                SalesOrderId = orderId,
                Number = "FV-501",
                InvoiceDate = DateTime.UtcNow,
                Lines = [new CreateCustomerInvoiceLineDto { SalesOrderLineId = lineId, BilledQuantity = 5m, UnitPrice = 10m }],
            }, CancellationToken.None));
    }
}

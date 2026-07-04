using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
using Erp.Modules.Purchasing.Application.Features.Invoices.Handlers;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class CreateSupplierInvoiceHandlerTests
{
    [Fact]
    public async Task Handle_Throws_WhenPurchaseOrderMissing()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var purchOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"si-po-miss-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"si-app-miss-{Guid.NewGuid()}")
            .Options;

        await using var purch = new PurchasingDbContext(purchOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);

        var handler = new CreateSupplierInvoiceHandler(purch, app, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateSupplierInvoiceCommand
            {
                PurchaseOrderId = Guid.NewGuid(),
                Number = "SI-1",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 100m,
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_WhenQuantityExceedsReceived()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var purchOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"si-qty-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"si-app-qty-{Guid.NewGuid()}")
            .Options;

        await using var purch = new PurchasingDbContext(purchOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
        await app.SaveChangesAsync();

        purch.PurchaseOrders.Add(new PurchaseOrder { Id = poId, CompanyId = companyId, Number = "PO-1", OrderDate = DateTime.UtcNow });
        purch.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = lineId, PurchaseOrderId = poId, Quantity = 10m, UnitPrice = 5m });
        purch.GoodsReceiptLines.Add(new GoodsReceiptLine
        {
            PurchaseOrderLineId = lineId,
            QuantityReceived = 3m,
            UnitPrice = 5m,
        });
        await purch.SaveChangesAsync();

        var handler = new CreateSupplierInvoiceHandler(purch, app, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateSupplierInvoiceCommand
            {
                PurchaseOrderId = poId,
                Number = "SI-2",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 50m,
                Lines =
                [
                    new CreateSupplierInvoiceLineDto
                    {
                        PurchaseOrderLineId = lineId,
                        Quantity = 5m,
                        UnitPrice = 5m,
                    },
                ],
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_Throws_WhenAmountBeyondTolerance()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var purchOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"si-amt-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"si-app-amt-{Guid.NewGuid()}")
            .Options;

        await using var purch = new PurchasingDbContext(purchOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674", MatchingToleranceAmount = 0m });
        await app.SaveChangesAsync();

        purch.PurchaseOrders.Add(new PurchaseOrder { Id = poId, CompanyId = companyId, Number = "PO-2", OrderDate = DateTime.UtcNow });
        purch.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = lineId, PurchaseOrderId = poId, Quantity = 10m, UnitPrice = 5m });
        purch.GoodsReceiptLines.Add(new GoodsReceiptLine { PurchaseOrderLineId = lineId, QuantityReceived = 10m, UnitPrice = 5m });
        await purch.SaveChangesAsync();

        var handler = new CreateSupplierInvoiceHandler(purch, app, tenant);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new CreateSupplierInvoiceCommand
            {
                PurchaseOrderId = poId,
                Number = "SI-3",
                InvoiceDate = DateTime.UtcNow,
                TotalAmount = 60m,
                Lines =
                [
                    new CreateSupplierInvoiceLineDto
                    {
                        PurchaseOrderLineId = lineId,
                        Quantity = 10m,
                        UnitPrice = 6m,
                    },
                ],
            }, CancellationToken.None));
    }

    [Fact]
    public async Task Handle_CreatesInvoice_WhenThreeWayMatchOk()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var purchOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"si-ok-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"si-app-ok-{Guid.NewGuid()}")
            .Options;

        await using var purch = new PurchasingDbContext(purchOptions, tenant);
        await using var app = new ErpDbContext(appOptions, tenant);
        app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674", MatchingToleranceAmount = 1m });
        await app.SaveChangesAsync();

        purch.PurchaseOrders.Add(new PurchaseOrder { Id = poId, CompanyId = companyId, Number = "PO-3", OrderDate = DateTime.UtcNow });
        purch.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = lineId, PurchaseOrderId = poId, Quantity = 10m, UnitPrice = 5m });
        purch.GoodsReceiptLines.Add(new GoodsReceiptLine { PurchaseOrderLineId = lineId, QuantityReceived = 10m, UnitPrice = 5m });
        await purch.SaveChangesAsync();

        var handler = new CreateSupplierInvoiceHandler(purch, app, tenant);
        var invoiceId = await handler.Handle(new CreateSupplierInvoiceCommand
        {
            PurchaseOrderId = poId,
            Number = "SI-OK",
            InvoiceDate = DateTime.UtcNow,
            TotalAmount = 50m,
            Lines =
            [
                new CreateSupplierInvoiceLineDto
                {
                    PurchaseOrderLineId = lineId,
                    Quantity = 10m,
                    UnitPrice = 5m,
                },
            ],
        }, CancellationToken.None);

        var saved = await purch.SupplierInvoices.Include(i => i.Lines).SingleAsync(i => i.Id == invoiceId);
        Assert.Equal("SI-OK", saved.Number);
        Assert.Single(saved.Lines);
    }
}

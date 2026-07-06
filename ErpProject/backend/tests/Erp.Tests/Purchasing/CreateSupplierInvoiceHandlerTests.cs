using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Purchasing.Application.Features.Invoices.Commands;
using Erp.Modules.Purchasing.Application.Features.Invoices.Handlers;
using Erp.Modules.Purchasing.Domain.Entities;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class CreateSupplierInvoiceHandlerTests
{
    private static (PurchasingDbContext purch, ErpDbContext app, FakeTenantContext tenant, FakeSupplierInfoService suppliers)
        CreateContexts(Guid companyId, Guid? supplierId = null)
    {
        var sid = supplierId ?? Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var purchOptions = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"si-{Guid.NewGuid()}")
            .Options;
        var appOptions = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"si-app-{Guid.NewGuid()}")
            .Options;

        var purch = new PurchasingDbContext(purchOptions, tenant);
        var app = new ErpDbContext(appOptions, tenant);
        var suppliers = new FakeSupplierInfoService(new Dictionary<Guid, SupplierInfoDto>
        {
            [sid] = new SupplierInfoDto("Proveedor Test", "B12345674", "prov@test.com", "Calle 1"),
        });

        return (purch, app, tenant, suppliers);
    }

    [Fact]
    public async Task Handle_Throws_WhenPurchaseOrderMissing()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var (purch, app, tenant, suppliers) = CreateContexts(companyId, supplierId);

        await using (purch)
        await using (app)
        {
            var handler = new CreateSupplierInvoiceHandler(purch, app, tenant, suppliers, new FakePublisher());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new CreateSupplierInvoiceCommand
                {
                    PurchaseOrderId = Guid.NewGuid(),
                    SupplierId = supplierId,
                    Number = "SI-1",
                    InvoiceDate = DateTime.UtcNow,
                    TotalAmount = 100m,
                }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Handle_Throws_WhenSupplierMissing()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var (purch, app, tenant, suppliers) = CreateContexts(companyId);

        await using (purch)
        await using (app)
        {
            app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
            await app.SaveChangesAsync();

            purch.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = poId, CompanyId = companyId, Number = "PO-1",
                OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatuses.Approved,
            });
            await purch.SaveChangesAsync();

            var handler = new CreateSupplierInvoiceHandler(purch, app, tenant, suppliers, new FakePublisher());
            await Assert.ThrowsAsync<ValidationException>(() =>
                handler.Handle(new CreateSupplierInvoiceCommand
                {
                    PurchaseOrderId = poId,
                    SupplierId = supplierId,
                    Number = "SI-1",
                    InvoiceDate = DateTime.UtcNow,
                    TotalAmount = 100m,
                }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Handle_Throws_WhenSupplierDiffersFromPurchaseOrder()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var poSupplierId = Guid.NewGuid();
        var otherSupplierId = Guid.NewGuid();
        var (purch, app, tenant, suppliers) = CreateContexts(companyId, poSupplierId);

        await using (purch)
        await using (app)
        {
            app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
            await app.SaveChangesAsync();

            purch.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = poId, CompanyId = companyId, SupplierId = poSupplierId,
                Number = "PO-1", OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatuses.Approved,
            });
            await purch.SaveChangesAsync();

            var handler = new CreateSupplierInvoiceHandler(purch, app, tenant, suppliers, new FakePublisher());
            await Assert.ThrowsAsync<ValidationException>(() =>
                handler.Handle(new CreateSupplierInvoiceCommand
                {
                    PurchaseOrderId = poId,
                    SupplierId = otherSupplierId,
                    Number = "SI-1",
                    InvoiceDate = DateTime.UtcNow,
                    TotalAmount = 100m,
                }, CancellationToken.None));
        }
    }

    [Fact]
    public async Task Handle_Throws_WhenQuantityExceedsReceived()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var (purch, app, tenant, suppliers) = CreateContexts(companyId, supplierId);

        await using (purch)
        await using (app)
        {
            app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674" });
            await app.SaveChangesAsync();

            purch.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = poId, CompanyId = companyId, SupplierId = supplierId,
                Number = "PO-1", OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatuses.Approved,
            });
            purch.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = lineId, PurchaseOrderId = poId, Quantity = 10m, UnitPrice = 5m });
            purch.GoodsReceiptLines.Add(new GoodsReceiptLine
            {
                PurchaseOrderLineId = lineId,
                QuantityReceived = 3m,
                UnitPrice = 5m,
            });
            await purch.SaveChangesAsync();

            var handler = new CreateSupplierInvoiceHandler(purch, app, tenant, suppliers, new FakePublisher());
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                handler.Handle(new CreateSupplierInvoiceCommand
                {
                    PurchaseOrderId = poId,
                    SupplierId = supplierId,
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
    }

    [Fact]
    public async Task Handle_CreatesInvoice_WhenThreeWayMatchOk()
    {
        var companyId = Guid.NewGuid();
        var poId = Guid.NewGuid();
        var lineId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var (purch, app, tenant, suppliers) = CreateContexts(companyId, supplierId);

        await using (purch)
        await using (app)
        {
            app.Companies.Add(new Company { Id = companyId, Name = "Co", TaxId = "B12345674", MatchingToleranceAmount = 1m });
            await app.SaveChangesAsync();

            purch.PurchaseOrders.Add(new PurchaseOrder
            {
                Id = poId, CompanyId = companyId, SupplierId = supplierId,
                Number = "PO-3", OrderDate = DateTime.UtcNow, Status = PurchaseOrderStatuses.Approved,
            });
            purch.PurchaseOrderLines.Add(new PurchaseOrderLine { Id = lineId, PurchaseOrderId = poId, Quantity = 10m, UnitPrice = 5m });
            purch.GoodsReceiptLines.Add(new GoodsReceiptLine { PurchaseOrderLineId = lineId, QuantityReceived = 10m, UnitPrice = 5m });
            await purch.SaveChangesAsync();

            var publisher = new FakePublisher();
            var handler = new CreateSupplierInvoiceHandler(purch, app, tenant, suppliers, publisher);
            var invoiceId = await handler.Handle(new CreateSupplierInvoiceCommand
            {
                PurchaseOrderId = poId,
                SupplierId = supplierId,
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
            Assert.Equal(supplierId, saved.SupplierId);
            Assert.Single(saved.Lines);

            var evt = Assert.Single(publisher.Published.OfType<SupplierInvoiceCreatedEvent>());
            Assert.Equal(invoiceId, evt.SupplierInvoiceId);
        }
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Features.Orders;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Purchasing;

public class CreatePurchaseOrderHandlerTests
{
    [Fact]
    public async Task Handle_PersistsOrderWithLinesAndSupplier()
    {
        var companyId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-create-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        var supplierInfo = new FakeSupplierInfoService(new Dictionary<Guid, SupplierInfoDto>
        {
            [supplierId] = new SupplierInfoDto("Proveedor CRM", "B12345674", "prov@test.com", "Calle 1"),
        });
        var handler = new CreatePurchaseOrderHandler(ctx, tenant, supplierInfo);

        var orderDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var result = await handler.Handle(new CreatePurchaseOrderCommand(
            supplierId,
            "PO-2026-001",
            orderDate,
            [new PurchaseOrderLineDto(Guid.NewGuid(), null, 10m, 25m)]), CancellationToken.None);

        Assert.Equal("PO-2026-001", result.Number);
        Assert.Equal(supplierId, result.SupplierId);
        Assert.Equal("Proveedor CRM", result.SupplierName);
        Assert.Single(result.Lines);
        Assert.Equal(10m, result.Lines[0].Quantity);

        var saved = await ctx.PurchaseOrders.Include(p => p.Lines).SingleAsync();
        Assert.Equal(companyId, saved.CompanyId);
        Assert.Equal(supplierId, saved.SupplierId);
    }

    [Fact]
    public async Task Handle_WithUnknownSupplier_ThrowsValidationException()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-create-invalid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        var handler = new CreatePurchaseOrderHandler(ctx, tenant, new FakeSupplierInfoService());

        var ex = await Assert.ThrowsAsync<ValidationException>(() => handler.Handle(
            new CreatePurchaseOrderCommand(
                Guid.NewGuid(),
                "PO-ERR",
                DateTime.UtcNow,
                []),
            CancellationToken.None));

        Assert.Contains(ex.Errors, e => e.PropertyName == nameof(CreatePurchaseOrderCommand.SupplierId));
    }
}

public class GetPurchaseOrderByIdHandlerTests
{
    [Fact]
    public async Task Handle_ReturnsOrderWithSupplierName_WhenExists()
    {
        var companyId = Guid.NewGuid();
        var orderId = Guid.NewGuid();
        var supplierId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var options = new DbContextOptionsBuilder<PurchasingDbContext>()
            .UseInMemoryDatabase($"purchasing-byid-{Guid.NewGuid()}")
            .Options;

        await using var ctx = new PurchasingDbContext(options, tenant);
        ctx.PurchaseOrders.Add(new Erp.Modules.Purchasing.Domain.Entities.PurchaseOrder
        {
            Id = orderId,
            CompanyId = companyId,
            SupplierId = supplierId,
            Number = "PO-99",
            OrderDate = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var supplierInfo = new FakeSupplierInfoService(new Dictionary<Guid, SupplierInfoDto>
        {
            [supplierId] = new SupplierInfoDto("Proveedor SL", "B12345674", "", ""),
        });
        var handler = new GetPurchaseOrderByIdHandler(ctx, tenant, supplierInfo);
        var result = await handler.Handle(new GetPurchaseOrderByIdQuery(orderId), CancellationToken.None);

        Assert.NotNull(result);
        Assert.Equal("PO-99", result!.Number);
        Assert.Equal("Proveedor SL", result.SupplierName);
    }
}

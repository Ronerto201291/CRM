using Erp.Modules.Crm.Application.Features.Crm.Commands;
using Erp.Modules.Crm.Application.Features.Crm.Handlers;
using Erp.Modules.Crm.Application.Features.Crm.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Crm;

/// <summary>
/// Cubre los campos nuevos PublicUploadEnabled/PublicUploadUrl (ADR-0018 #39) añadidos a
/// SupplierDto/SupplierHandlers — no reintenta cubrir el resto de SupplierHandlers, que ya
/// no tenía tests previos.
/// </summary>
public class SupplierPublicUploadFieldsTests
{
    private static CrmDbContext NewContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"supplier-public-upload-{Guid.NewGuid()}")
            .Options,
        tenant);

    private sealed class NoopPublisher : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default) where TNotification : INotification
            => Task.CompletedTask;
    }

    [Fact]
    public async Task GetSuppliers_WithEnabledUpload_ExposesPublicUploadUrl()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = "Proveedor Test", TaxId = "B12345678",
            Email = "p@test.com", Phone = "600000000", Address = "Calle 1",
            PublicUploadToken = token, PublicUploadEnabled = true,
        });
        await ctx.SaveChangesAsync();

        var portalUrlProvider = new FakePortalUrlProvider { PortalBaseUrl = "https://portal.test.example/" };
        var handler = new GetSuppliersHandler(ctx, portalUrlProvider);

        var result = await handler.Handle(new GetSuppliersQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.True(dto.PublicUploadEnabled);
        Assert.Equal($"https://portal.test.example/proveedor/{token}", dto.PublicUploadUrl);
    }

    [Fact]
    public async Task GetSuppliers_WithDisabledUpload_PublicUploadUrlIsNull()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        ctx.Suppliers.Add(new Supplier
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = "Proveedor Test", TaxId = "B12345678",
            Email = "p@test.com", Phone = "600000000", Address = "Calle 1",
            PublicUploadToken = Guid.NewGuid().ToString("N"), PublicUploadEnabled = false,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSuppliersHandler(ctx, new FakePortalUrlProvider());
        var result = await handler.Handle(new GetSuppliersQuery(), CancellationToken.None);

        var dto = Assert.Single(result.Items);
        Assert.False(dto.PublicUploadEnabled);
        Assert.Null(dto.PublicUploadUrl);
    }

    [Fact]
    public async Task UpdateSupplier_TogglingPublicUploadEnabled_PersistsAndReturnsInDto()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var supplier = new Supplier
        {
            Id = Guid.NewGuid(), CompanyId = companyId, Name = "Proveedor Test", TaxId = "B12345678",
            Email = "p@test.com", Phone = "600000000", Address = "Calle 1",
            PublicUploadToken = Guid.NewGuid().ToString("N"), PublicUploadEnabled = false,
        };
        ctx.Suppliers.Add(supplier);
        await ctx.SaveChangesAsync();

        var handler = new UpdateSupplierHandler(ctx, new FakePortalUrlProvider { PortalBaseUrl = "https://portal.test.example" });
        var result = await handler.Handle(new UpdateSupplierCommand
        {
            Id = supplier.Id, Name = supplier.Name, TaxId = supplier.TaxId, Email = supplier.Email,
            Phone = supplier.Phone, Address = supplier.Address, PublicUploadEnabled = true,
        }, CancellationToken.None);

        Assert.NotNull(result);
        Assert.True(result!.PublicUploadEnabled);
        Assert.Equal($"https://portal.test.example/proveedor/{supplier.PublicUploadToken}", result.PublicUploadUrl);

        var updated = await ctx.Suppliers.SingleAsync(s => s.Id == supplier.Id);
        Assert.True(updated.PublicUploadEnabled);
    }

    [Fact]
    public async Task CreateSupplier_DefaultsToPublicUploadEnabledWithUrl()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);

        var handler = new CreateSupplierHandler(ctx, tenant, new NoopPublisher(), new FakePortalUrlProvider { PortalBaseUrl = "https://portal.test.example" });
        var result = await handler.Handle(new CreateSupplierCommand
        {
            Name = "Nuevo Proveedor", TaxId = "B87654321", Email = "n@test.com", Phone = "600", Address = "Calle 2",
        }, CancellationToken.None);

        Assert.True(result.PublicUploadEnabled);
        Assert.NotNull(result.PublicUploadUrl);
        Assert.StartsWith("https://portal.test.example/proveedor/", result.PublicUploadUrl);
    }
}

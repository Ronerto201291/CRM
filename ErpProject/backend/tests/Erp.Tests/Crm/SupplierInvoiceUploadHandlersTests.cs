using Erp.Modules.Crm.Application.Features.SupplierUploads.Commands;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Handlers;
using Erp.Modules.Crm.Application.Features.SupplierUploads.Queries;
using Erp.Modules.Crm.Domain.Entities;
using Erp.Modules.Crm.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Erp.Tests.Crm;

public class SupplierInvoiceUploadHandlersTests
{
    private static CrmDbContext NewContext(FakeTenantContext tenant) => new(
        new DbContextOptionsBuilder<CrmDbContext>()
            .UseInMemoryDatabase($"supplier-uploads-{Guid.NewGuid()}")
            .Options,
        tenant);

    private static IConfiguration EmptyConfig() => new ConfigurationBuilder().Build();

    private static Supplier CreateSupplier(Guid companyId, string token, bool enabled = true) => new()
    {
        Id = Guid.NewGuid(), CompanyId = companyId, Name = "Proveedor Test", TaxId = "B12345678",
        Email = "prov@test.com", Phone = "600000000", Address = "Calle Test 1",
        PublicUploadToken = token, PublicUploadEnabled = enabled,
    };

    [Fact]
    public async Task Upload_WithValidEnabledToken_CreatesUploadRow()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext(); // token-based upload has no resolved tenant
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        var supplier = CreateSupplier(companyId, token);
        ctx.Suppliers.Add(supplier);
        await ctx.SaveChangesAsync();

        var storage = new FakeFileStorageService();
        var handler = new UploadSupplierInvoiceByTokenHandler(ctx, storage, EmptyConfig());

        var result = await handler.Handle(new UploadSupplierInvoiceByTokenCommand
        {
            Token = token,
            FileName = "factura.pdf",
            ContentType = "application/pdf",
            FileContent = [0x25, 0x50, 0x44, 0x46],
            Comment = "Factura de marzo",
        }, CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UploadId);
        var upload = await ctx.SupplierInvoiceUploads.IgnoreQueryFilters().SingleAsync(u => u.Id == result.UploadId);
        Assert.Equal(supplier.Id, upload.SupplierId);
        Assert.Equal(companyId, upload.CompanyId);
        Assert.Equal("Pending", upload.Status);
        Assert.Equal("Factura de marzo", upload.Comment);
        Assert.False(string.IsNullOrEmpty(upload.FilePath));
    }

    [Fact]
    public async Task Upload_WithDisabledToken_ThrowsKeyNotFoundException()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Suppliers.Add(CreateSupplier(companyId, token, enabled: false));
        await ctx.SaveChangesAsync();

        var handler = new UploadSupplierInvoiceByTokenHandler(ctx, new FakeFileStorageService(), EmptyConfig());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new UploadSupplierInvoiceByTokenCommand { Token = token, FileName = "f.pdf", ContentType = "application/pdf", FileContent = [1] },
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_WithUnknownToken_ThrowsKeyNotFoundException()
    {
        var tenant = new FakeTenantContext();
        await using var ctx = NewContext(tenant);
        var handler = new UploadSupplierInvoiceByTokenHandler(ctx, new FakeFileStorageService(), EmptyConfig());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new UploadSupplierInvoiceByTokenCommand { Token = Guid.NewGuid().ToString("N"), FileName = "f.pdf", ContentType = "application/pdf", FileContent = [1] },
            CancellationToken.None));
    }

    [Fact]
    public async Task Upload_IsCrossTenantByDesign_TokenAloneIsTheOnlyGuard()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext(); // TenantId left unset (null) on purpose
        await using var ctx = NewContext(tenant);
        var token = Guid.NewGuid().ToString("N");
        ctx.Suppliers.Add(CreateSupplier(companyId, token));
        await ctx.SaveChangesAsync();

        var handler = new UploadSupplierInvoiceByTokenHandler(ctx, new FakeFileStorageService(), EmptyConfig());
        var result = await handler.Handle(
            new UploadSupplierInvoiceByTokenCommand { Token = token, FileName = "f.pdf", ContentType = "application/pdf", FileContent = [1] },
            CancellationToken.None);

        Assert.NotEqual(Guid.Empty, result.UploadId);
    }

    [Fact]
    public async Task MarkReviewed_WithExistingUpload_SetsStatusReviewed()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var supplier = CreateSupplier(companyId, Guid.NewGuid().ToString("N"));
        ctx.Suppliers.Add(supplier);
        var upload = new SupplierInvoiceUpload
        {
            Id = Guid.NewGuid(), CompanyId = companyId, SupplierId = supplier.Id,
            PublicTokenUsed = supplier.PublicUploadToken, FileName = "f.pdf", FilePath = "x/y.pdf",
            ContentType = "application/pdf", Status = "Pending",
        };
        ctx.SupplierInvoiceUploads.Add(upload);
        await ctx.SaveChangesAsync();

        var handler = new MarkSupplierInvoiceUploadReviewedHandler(ctx);
        var ok = await handler.Handle(new MarkSupplierInvoiceUploadReviewedCommand { Id = upload.Id }, CancellationToken.None);

        Assert.True(ok);
        var updated = await ctx.SupplierInvoiceUploads.SingleAsync(u => u.Id == upload.Id);
        Assert.Equal("Reviewed", updated.Status);
    }

    [Fact]
    public async Task MarkReviewed_WithUnknownId_ReturnsFalse()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new MarkSupplierInvoiceUploadReviewedHandler(ctx);

        var ok = await handler.Handle(new MarkSupplierInvoiceUploadReviewedCommand { Id = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(ok);
    }

    [Fact]
    public async Task GetUploads_ReturnsOrderedListWithSupplierName()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var supplier = CreateSupplier(companyId, Guid.NewGuid().ToString("N"));
        ctx.Suppliers.Add(supplier);
        ctx.SupplierInvoiceUploads.Add(new SupplierInvoiceUpload
        {
            Id = Guid.NewGuid(), CompanyId = companyId, SupplierId = supplier.Id,
            PublicTokenUsed = supplier.PublicUploadToken, FileName = "old.pdf", FilePath = "x/old.pdf",
            ContentType = "application/pdf", Status = "Pending", UploadedAt = DateTime.UtcNow.AddDays(-1),
        });
        ctx.SupplierInvoiceUploads.Add(new SupplierInvoiceUpload
        {
            Id = Guid.NewGuid(), CompanyId = companyId, SupplierId = supplier.Id,
            PublicTokenUsed = supplier.PublicUploadToken, FileName = "new.pdf", FilePath = "x/new.pdf",
            ContentType = "application/pdf", Status = "Pending", UploadedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetSupplierInvoiceUploadsHandler(ctx);
        var result = await handler.Handle(new GetSupplierInvoiceUploadsQuery(), CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("new.pdf", result[0].FileName);
        Assert.Equal(supplier.Name, result[0].SupplierName);
    }

    [Fact]
    public async Task GetDownloadUrl_WithExistingUpload_ReturnsSignedUrl()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var supplier = CreateSupplier(companyId, Guid.NewGuid().ToString("N"));
        ctx.Suppliers.Add(supplier);
        var upload = new SupplierInvoiceUpload
        {
            Id = Guid.NewGuid(), CompanyId = companyId, SupplierId = supplier.Id,
            PublicTokenUsed = supplier.PublicUploadToken, FileName = "f.pdf", FilePath = "abc/f.pdf",
            ContentType = "application/pdf", Status = "Pending",
        };
        ctx.SupplierInvoiceUploads.Add(upload);
        await ctx.SaveChangesAsync();

        var handler = new GetSupplierInvoiceUploadDownloadUrlHandler(ctx, new FakeFileStorageService(), EmptyConfig());
        var url = await handler.Handle(new GetSupplierInvoiceUploadDownloadUrlQuery { Id = upload.Id }, CancellationToken.None);

        Assert.Contains("abc/f.pdf", url);
    }

    [Fact]
    public async Task GetDownloadUrl_WithUnknownId_ThrowsKeyNotFoundException()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new GetSupplierInvoiceUploadDownloadUrlHandler(ctx, new FakeFileStorageService(), EmptyConfig());

        await Assert.ThrowsAsync<KeyNotFoundException>(() => handler.Handle(
            new GetSupplierInvoiceUploadDownloadUrlQuery { Id = Guid.NewGuid() }, CancellationToken.None));
    }
}

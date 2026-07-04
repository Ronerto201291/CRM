using Erp.Application.Features.Documents.Commands;
using Erp.Application.Features.Documents.Handlers;
using Erp.Application.Features.Documents.Queries;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Documents;

public class DocumentHandlersTests
{
    private static ErpDbContext NewContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"documents-{Guid.NewGuid()}")
            .Options;
        return new ErpDbContext(options, tenant);
    }

    [Fact]
    public async Task Upload_WithValidPdf_PersistsDocumentAndStoresContent()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var storage = new FakeFileStorageService();
        var handler = new UploadDocumentHandler(ctx, tenant, storage);

        var result = await handler.Handle(new UploadDocumentCommand
        {
            UploadedByUserId = Guid.NewGuid(),
            FileName = "contrato.pdf",
            ContentType = "application/pdf",
            Content = [1, 2, 3, 4],
            EntityType = "Client",
            EntityId = Guid.NewGuid(),
            Description = "Contrato de mantenimiento",
        }, CancellationToken.None);

        Assert.Equal("contrato.pdf", result.FileName);
        Assert.Equal(4, result.SizeBytes);
        var stored = await ctx.Documents.SingleAsync();
        Assert.Equal(companyId, stored.CompanyId);
        Assert.Single(storage.Objects);
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, storage.Objects.Values.Single());
    }

    [Fact]
    public async Task Upload_WithDisallowedContentType_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new UploadDocumentHandler(ctx, tenant, new FakeFileStorageService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new UploadDocumentCommand
        {
            FileName = "virus.exe",
            ContentType = "application/x-msdownload",
            Content = [1, 2, 3],
        }, CancellationToken.None));
    }

    [Fact]
    public async Task Upload_WithEmptyContent_Throws()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Test");
        await using var ctx = NewContext(tenant);
        var handler = new UploadDocumentHandler(ctx, tenant, new FakeFileStorageService());

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.Handle(new UploadDocumentCommand
        {
            FileName = "vacio.pdf",
            ContentType = "application/pdf",
            Content = [],
        }, CancellationToken.None));
    }

    [Fact]
    public async Task GetDocuments_FiltersByCompanyAndEntity()
    {
        var tenant = new FakeTenantContext();
        var companyA = Guid.NewGuid();
        var companyB = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        tenant.SetTenant(companyA, "Empresa A");
        await using var ctx = NewContext(tenant);

        ctx.Documents.AddRange(
            new Document { CompanyId = companyA, FileName = "a1.pdf", ContentType = "application/pdf", ObjectKey = "k1", EntityType = "Client", EntityId = clientId },
            new Document { CompanyId = companyA, FileName = "a2.pdf", ContentType = "application/pdf", ObjectKey = "k2", EntityType = "Client", EntityId = Guid.NewGuid() },
            new Document { CompanyId = companyB, FileName = "b1.pdf", ContentType = "application/pdf", ObjectKey = "k3", EntityType = "Client", EntityId = clientId });
        await ctx.SaveChangesAsync();

        var handler = new GetDocumentsHandler(ctx, tenant);
        var result = await handler.Handle(new GetDocumentsQuery { EntityType = "Client", EntityId = clientId }, CancellationToken.None);

        Assert.Equal(1, result.TotalCount);
        Assert.Equal("a1.pdf", result.Items.Single().FileName);
    }

    [Fact]
    public async Task Delete_RemovesFromDbAndStorage()
    {
        var tenant = new FakeTenantContext();
        var companyId = Guid.NewGuid();
        tenant.SetTenant(companyId, "Empresa Test");
        await using var ctx = NewContext(tenant);
        var storage = new FakeFileStorageService();
        storage.Objects["k1"] = [1, 2, 3];

        var docId = Guid.NewGuid();
        ctx.Documents.Add(new Document { Id = docId, CompanyId = companyId, FileName = "a.pdf", ContentType = "application/pdf", ObjectKey = "k1" });
        await ctx.SaveChangesAsync();

        var handler = new DeleteDocumentHandler(ctx, tenant, storage);
        var ok = await handler.Handle(new DeleteDocumentCommand { Id = docId }, CancellationToken.None);

        Assert.True(ok);
        Assert.Empty(ctx.Documents);
        Assert.Contains("k1", storage.DeletedKeys);
    }

    [Fact]
    public async Task Delete_FromOtherCompany_ReturnsFalseAndDoesNotDelete()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa Atacante");
        await using var ctx = NewContext(tenant);
        var storage = new FakeFileStorageService();

        var docId = Guid.NewGuid();
        ctx.Documents.Add(new Document { Id = docId, CompanyId = Guid.NewGuid(), FileName = "otra-empresa.pdf", ContentType = "application/pdf", ObjectKey = "k1" });
        await ctx.SaveChangesAsync();

        var handler = new DeleteDocumentHandler(ctx, tenant, storage);
        var ok = await handler.Handle(new DeleteDocumentCommand { Id = docId }, CancellationToken.None);

        Assert.False(ok);
    }
}

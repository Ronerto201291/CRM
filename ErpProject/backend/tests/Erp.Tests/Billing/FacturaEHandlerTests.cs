using Erp.Modules.Billing.Application.Features.Billing.Queries;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Billing;

public class FacturaEHandlerTests
{
    private static BillingDbContext CreateBillingContext(FakeTenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"facturae-{Guid.NewGuid()}")
            .Options;
        return new BillingDbContext(options, tenant);
    }

    [Fact]
    public async Task GenerateFacturaE_ReturnsXmlFromService()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var facturaE = new FakeFacturaEService();
        var handler = new GenerateFacturaEHandler(facturaE, tenant);

        var result = await handler.Handle(new GenerateFacturaEQuery(invoiceId), CancellationToken.None);

        Assert.Equal("factura-test.xml", result.FileName);
        Assert.NotEmpty(result.XmlBytes);
    }

    [Fact]
    public async Task GenerateFacturaE_Throws_WhenTenantMissing()
    {
        var tenant = new FakeTenantContext();
        var handler = new GenerateFacturaEHandler(new FakeFacturaEService(), tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new GenerateFacturaEQuery(Guid.NewGuid()), CancellationToken.None));
    }

    [Fact]
    public async Task GenerateSignedFacturaE_ReturnsSignedFileName()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var handler = new GenerateSignedFacturaEHandler(new FakeFacturaEService(), tenant);
        var result = await handler.Handle(new GenerateSignedFacturaEQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Contains("signed", result.FileName);
    }

    [Fact]
    public async Task ValidateFacturaE_ReturnsValidationResult()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var handler = new ValidateFacturaEHandler(new FakeFacturaEService(), tenant);
        var result = await handler.Handle(new ValidateFacturaEQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.Equal("factura-test.xml", result.FileName);
        Assert.NotNull(result.Errors);
    }

    [Fact]
    public async Task SubmitFacturaEFace_DelegatesToFaceService()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var face = new FakeFaceSubmissionService();
        var handler = new SubmitFacturaEFaceHandler(new FakeFacturaEService(), face, tenant);
        var result = await handler.Handle(new SubmitFacturaEFaceCommand(Guid.NewGuid()), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("FACE-REF-001", result.ReferenceId);
    }

    [Fact]
    public async Task GetVerifactuSubmissions_ReturnsLogsForInvoice()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        await using var ctx = CreateBillingContext(tenant);
        ctx.VerifactuSubmissionLogs.Add(new VerifactuSubmissionLog
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            InvoiceId = invoiceId,
            InvoiceNumber = "F-2026-001",
            SubmissionType = "Alta",
            EstadoEnvio = "Correcto",
            Success = true,
            IsProduction = false,
            SubmittedAt = DateTime.UtcNow,
        });
        await ctx.SaveChangesAsync();

        var handler = new GetVerifactuSubmissionsHandler(ctx);
        var logs = await handler.Handle(new GetVerifactuSubmissionsQuery(invoiceId), CancellationToken.None);

        Assert.Single(logs);
        Assert.Equal("Alta", logs[0].SubmissionType);
        Assert.True(logs[0].Success);
    }
}

using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Features.Sii;
using Erp.Infrastructure.Services.Sii;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.Tests.Fiscal;

public class SiiSubmissionHandlerTests
{
    [Fact]
    public async Task SubmitSiiHandler_Throws_WhenSignerNotConfigured()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var generator = CreateGenerator(tenant.TenantId!.Value);
        var signer = new FakeSiiSigningService(configured: false);
        var submission = new SiiSubmissionService(
            CreateConfig(sendEnabled: false), NullLogger<SiiSubmissionService>.Instance,
            new FakeHttpClientFactory(_ => throw new InvalidOperationException()));

        var handler = new SubmitSiiHandler(generator, signer, submission, tenant);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitSiiCommand("emitidas", 2026, 7), CancellationToken.None));
    }

    [Fact]
    public async Task SubmitSiiHandler_ReturnsSuccess_WithMockedSignerAndAeat()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var generator = CreateGenerator(companyId);
        var signer = new FakeSiiSigningService();
        var config = CreateConfig(sendEnabled: true);
        var submission = new SiiSubmissionService(
            config, NullLogger<SiiSubmissionService>.Instance,
            FakeHttpClientFactory.AeatSuccess("Correcto"));

        var handler = new SubmitSiiHandler(generator, signer, submission, tenant);
        var result = await handler.Handle(new SubmitSiiCommand("emitidas", 2026, 7), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Correcto", result.Estado);
        Assert.Equal("2026-07", result.Period);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task SubmitSiiHandler_ReturnsFailure_WhenAeatRejects()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var generator = CreateGenerator(companyId);
        var signer = new FakeSiiSigningService();
        var submission = new SiiSubmissionService(
            CreateConfig(sendEnabled: true), NullLogger<SiiSubmissionService>.Instance,
            FakeHttpClientFactory.AeatFailure());

        var handler = new SubmitSiiHandler(generator, signer, submission, tenant);
        var result = await handler.Handle(new SubmitSiiCommand("recibidas", 2026, 6), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
        Assert.Equal("2026-06", result.Period);
    }

    [Fact]
    public async Task PreviewSiiHandler_ReturnsBase64XmlForBothTypes()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var handler = new PreviewSiiHandler(CreateGenerator(companyId), tenant);
        var result = await handler.Handle(new PreviewSiiQuery(2026, 7), CancellationToken.None);

        Assert.Equal("2026-07", result.Period);
        Assert.False(string.IsNullOrEmpty(result.FacturasEmitidasXml));
        Assert.False(string.IsNullOrEmpty(result.FacturasRecibidasXml));
    }

    [Fact]
    public async Task GetSiiEmitidasXmlHandler_ReturnsXmlFile()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var handler = new GetSiiEmitidasXmlHandler(CreateGenerator(companyId), tenant);
        var result = await handler.Handle(new GetSiiEmitidasXmlQuery(2026, 7), CancellationToken.None);

        Assert.Equal("SII_FacturasEmitidas_2026_07.xml", result.FileName);
        Assert.Contains("<?xml", Encoding.UTF8.GetString(result.Bytes));
    }

    [Fact]
    public async Task SubmitVerifactuHandler_ReturnsSuccess_WithMockedAeat()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var httpFactory = FakeHttpClientFactory.AeatSuccess("Correcto");
        var submission = new VerifactuSubmissionService(
            httpFactory, NullLogger<VerifactuSubmissionService>.Instance);

        var handler = new SubmitVerifactuHandler(
            new FakeVerifactuXmlGenerator(), submission, tenant);

        var result = await handler.Handle(new SubmitVerifactuCommand(2026, 7, false), CancellationToken.None);

        Assert.True(result.Success);
        Assert.Equal("Correcto", result.EstadoEnvio);
        Assert.Equal("2026-07", result.Period);
        Assert.Null(result.Error);
    }

    [Fact]
    public async Task SubmitVerifactuHandler_ReturnsFailure_WhenAeatRejects()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var httpFactory = FakeHttpClientFactory.AeatFailure();
        var submission = new VerifactuSubmissionService(
            httpFactory, NullLogger<VerifactuSubmissionService>.Instance);

        var handler = new SubmitVerifactuHandler(
            new FakeVerifactuXmlGenerator(), submission, tenant);

        var result = await handler.Handle(new SubmitVerifactuCommand(2026, 7, false), CancellationToken.None);

        Assert.False(result.Success);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task SubmitVerifactuHandler_Throws_WithoutTenant()
    {
        var submission = new VerifactuSubmissionService(
            FakeHttpClientFactory.AeatSuccess(),
            NullLogger<VerifactuSubmissionService>.Instance);

        var handler = new SubmitVerifactuHandler(
            new FakeVerifactuXmlGenerator(), submission, new FakeTenantContext());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(new SubmitVerifactuCommand(2026, 7), CancellationToken.None));
    }

    [Fact]
    public async Task ValidateSiiXmlHandler_ReturnsValidationResult()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");

        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>()).Build();
        var generator = CreateGenerator(companyId);
        var signer = new FakeSiiSigningService(configured: false);

        var handler = new ValidateSiiXmlHandler(generator, signer, tenant);
        var result = await handler.Handle(new ValidateSiiXmlQuery(2026, 7, "emitidas"), CancellationToken.None);

        Assert.Equal("2026-07", result.Period);
        Assert.Equal("emitidas", result.Type);
        Assert.False(result.SignerConfigured);
    }

    [Fact]
    public async Task GetVerifactuXmlHandler_ReturnsXmlFile()
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(Guid.NewGuid(), "Empresa test");

        var handler = new GetVerifactuXmlHandler(new FakeVerifactuXmlGenerator(), tenant);
        var result = await handler.Handle(new GetVerifactuXmlQuery(2026, 7), CancellationToken.None);

        Assert.Equal("Verifactu_2026_07.xml", result.FileName);
        Assert.Contains("<Verifactu>", Encoding.UTF8.GetString(result.Bytes));
    }

    private static IConfiguration CreateConfig(bool sendEnabled)
        => new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sii:SendEnabled"] = sendEnabled.ToString(),
                ["Sii:Environment"] = "test",
            })
            .Build();

    private static SiiXmlGenerator CreateGenerator(Guid companyId)
    {
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa test");
        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"sii-gen-{Guid.NewGuid()}")
            .Options;
        var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa test",
            TaxId = "B12345674",
            Address = "Calle 1",
            CreatedAt = DateTime.UtcNow,
        });
        ctx.SaveChanges();

        return new SiiXmlGenerator(ctx, new FakeSiiEmitidasSource(), new FakeSiiRecibidasSource());
    }

    private sealed class FakeSiiEmitidasSource : ISiiEmitidasInvoiceSource
    {
        public Task<IReadOnlyList<SiiEmitidaInvoiceDto>> GetLockedInvoicesAsync(
            Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SiiEmitidaInvoiceDto>>(Array.Empty<SiiEmitidaInvoiceDto>());
    }

    private sealed class FakeSiiRecibidasSource : ISiiRecibidasExpenseSource
    {
        public Task<IReadOnlyList<SiiRecibidaExpenseDto>> GetApprovedExpensesAsync(
            Guid companyId, DateTime periodStart, DateTime periodEnd, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<SiiRecibidaExpenseDto>>(Array.Empty<SiiRecibidaExpenseDto>());
    }
}

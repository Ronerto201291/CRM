using System.IO.Compression;
using System.Text;
using Erp.Application.Common;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Modules.Billing.Application.Features.Billing.Commands;
using Erp.Modules.Billing.Application.Features.Billing.Handlers;
using Erp.Modules.Billing.Domain.Entities;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Billing.Infrastructure.Services;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Erp.Tests.Fiscal;

public class VerifactuAnulacionRegistrarTests
{
    [Fact]
    public async Task Register_ComputesAnulacionHuella_AndEnqueues()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa VF");

        await using var billing = VerifactuSifTestDb.CreateBilling(tenant);
        await using var app = VerifactuSifTestDb.CreateApp(tenant, companyId);

        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000001",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 1,
            IssueDate = new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = DateTime.UtcNow.AddDays(30),
            Subtotal = 100m,
            TaxAmount = 21m,
            Total = 121m,
            IsLocked = true,
            LockedAt = DateTime.UtcNow.AddHours(-1),
            VerifactuHuella = "ALTA-HUELLA-1",
            VerifactuRealtimeSubmission = true,
        });
        await billing.SaveChangesAsync();

        var gateway = new FakeVerifactuSubmissionGateway();
        var registrar = new VerifactuAnulacionRegistrar(
            billing, app, new FakeVerifactuService(), gateway, NullLogger<VerifactuAnulacionRegistrar>.Instance);

        var ok = await registrar.RegisterAsync(invoiceId, CancellationToken.None);

        Assert.True(ok);
        var inv = await billing.Invoices.SingleAsync();
        Assert.NotNull(inv.VerifactuAnulacionHuella);
        Assert.StartsWith("anul-A-2026-000001", inv.VerifactuAnulacionHuella);
        Assert.Contains(invoiceId, gateway.EnqueuedAnulaciones);
    }

    [Fact]
    public async Task AnulVerifactuHandler_DelegatesToRegistrar()
    {
        var registrar = new FakeVerifactuAnulacionRegistrar();
        var handler = new AnulVerifactuInvoiceHandler(registrar);
        var id = Guid.NewGuid();

        await handler.Handle(new AnulVerifactuInvoiceCommand { InvoiceId = id }, CancellationToken.None);

        Assert.Contains(id, registrar.Registered);
    }
}

public class VerifactuServiceAnulacionTests
{
    [Fact]
    public void ComputeAnulacionHuella_UsesFiveOfficialFields()
    {
        var svc = new VerifactuService(Options.Create(new VerifactuOptions
        {
            NifSoftware = "B12345674",
            NombreSoftware = "Test ERP",
            IdSistema = "TEST-1",
            Version = "1.0",
            NumeroInstalacion = "1"
        }));

        var h1 = svc.ComputeAnulacionHuella(
            "B12345674", "FAC-001", new DateOnly(2026, 3, 15), null,
            new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));
        var h2 = svc.ComputeAnulacionHuella(
            "B12345674", "FAC-001", new DateOnly(2026, 3, 15), "PREV",
            new DateTimeOffset(2026, 3, 15, 10, 0, 0, TimeSpan.Zero));

        Assert.Equal(64, h1.Length);
        Assert.NotEqual(h1, h2);
    }
}

public class VerifactuTipoFacturaTests
{
    [Theory]
    [InlineData("Simplificada", null, "F2")]
    [InlineData("Rectificativa", "Normal", "R1")]
    [InlineData("Rectificativa", "Simplificada", "R5")]
    [InlineData("Normal", null, "F1")]
    public void Resolve_MapsInvoiceTypes(string type, string? origType, string expected)
        => Assert.Equal(expected, VerifactuTipoFactura.Resolve(type, origType));
}

public class VerifactuXmlGeneratorTipoTests
{
    [Fact]
    public async Task GenerateSingle_IncludesRectificativaAndSimplificadaMarkers()
    {
        var companyId = Guid.NewGuid();
        var origId = Guid.NewGuid();
        var rectId = Guid.NewGuid();
        var simpId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        await using var billing = VerifactuSifTestDb.CreateBilling(tenant);
        await using var app = VerifactuSifTestDb.CreateApp(tenant, companyId);

        billing.Invoices.AddRange(
            new Invoice
            {
                Id = origId,
                CompanyId = companyId,
                Number = "A-2026-000010",
                Series = "A",
                FiscalYear = 2026,
                SequenceNumber = 10,
                InvoiceType = "Normal",
                IssueDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                DueDate = DateTime.UtcNow,
                Subtotal = 100m,
                TaxAmount = 21m,
                Total = 121m,
                Status = "Locked",
                IsLocked = true,
                LockedAt = DateTime.UtcNow.AddDays(-2),
                VerifactuHuella = "H-ORIG",
            },
            new Invoice
            {
                Id = rectId,
                CompanyId = companyId,
                Number = "R-2026-000001",
                Series = "R",
                FiscalYear = 2026,
                SequenceNumber = 1,
                InvoiceType = "Rectificativa",
                RectifiedInvoiceId = origId,
                IssueDate = new DateTime(2026, 6, 2, 0, 0, 0, DateTimeKind.Utc),
                DueDate = DateTime.UtcNow,
                Subtotal = -100m,
                TaxAmount = -21m,
                Total = -121m,
                Status = "Locked",
                IsLocked = true,
                LockedAt = DateTime.UtcNow.AddDays(-1),
                VerifactuHuella = "H-RECT",
                InvoiceLines =
                [
                    new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        Description = "Rect",
                        Quantity = -1,
                        UnitPrice = 100,
                        TaxRate = 21,
                        TaxAmount = -21,
                        LineTotal = -100,
                    },
                ],
            },
            new Invoice
            {
                Id = simpId,
                CompanyId = companyId,
                Number = "S-2026-000001",
                Series = "S",
                FiscalYear = 2026,
                SequenceNumber = 1,
                InvoiceType = "Simplificada",
                IssueDate = new DateTime(2026, 6, 3, 0, 0, 0, DateTimeKind.Utc),
                DueDate = DateTime.UtcNow,
                Subtotal = 50m,
                TaxAmount = 10.5m,
                Total = 60.5m,
                Status = "Locked",
                IsLocked = true,
                LockedAt = DateTime.UtcNow,
                VerifactuHuella = "H-SIMP",
                InvoiceLines =
                [
                    new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        Description = "Ticket",
                        Quantity = 1,
                        UnitPrice = 50,
                        TaxRate = 21,
                        TaxAmount = 10.5m,
                        LineTotal = 50,
                    },
                ],
            });
        await billing.SaveChangesAsync();

        var gen = new VerifactuXmlGenerator(
            billing,
            app,
            new FakeVerifactuService(),
            Options.Create(new VerifactuOptions
            {
                NifSoftware = "B12345674",
                NombreSoftware = "Test ERP",
                IdSistema = "TEST-1",
            }));

        var rectXml = await gen.GenerateSingleInvoiceRegistroAsync(rectId, CancellationToken.None);
        Assert.Contains("<sf:TipoFactura>R1</sf:TipoFactura>", rectXml);
        Assert.Contains("FacturasRectificadas", rectXml);
        Assert.Contains("A-2026-000010", rectXml);
        Assert.Contains("<sf:TipoRectificativa>S</sf:TipoRectificativa>", rectXml);

        var simpXml = await gen.GenerateSingleInvoiceRegistroAsync(simpId, CancellationToken.None);
        Assert.Contains("<sf:TipoFactura>F2</sf:TipoFactura>", simpXml);
    }

    [Fact]
    public async Task GenerateAnulacion_UsesAnulacionHuella_NotAltaHuella()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        await using var billing = VerifactuSifTestDb.CreateBilling(tenant);
        await using var app = VerifactuSifTestDb.CreateApp(tenant, companyId);

        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000099",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 99,
            IssueDate = new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc),
            DueDate = DateTime.UtcNow,
            Total = 121m,
            TaxAmount = 21m,
            Subtotal = 100m,
            IsLocked = true,
            LockedAt = DateTime.UtcNow.AddHours(-2),
            VerifactuHuella = "HUELLA-ALTA-ORIGINAL",
            VerifactuAnulacionHuella = "HUELLA-ANULACION-NUEVA",
            VerifactuAnulacionAt = DateTime.UtcNow,
        });
        await billing.SaveChangesAsync();

        var gen = new VerifactuXmlGenerator(
            billing, app, new FakeVerifactuService(),
            Options.Create(new VerifactuOptions
            {
                NifSoftware = "B12345674",
                NombreSoftware = "Test ERP",
                IdSistema = "TEST-1",
            }));

        var xml = await gen.GenerateAnulacionRegistroAsync(invoiceId, CancellationToken.None);
        Assert.Contains("RegistroAnulacion", xml);
        Assert.Contains("<sf:Huella>HUELLA-ANULACION-NUEVA</sf:Huella>", xml);
        // La huella de alta puede aparecer en RegistroAnterior (encadenamiento) — es correcto.
    }
}

public class VerifactuConservationExporterTests
{
    [Fact]
    public async Task Export_BuildsZipWithManifestAndAltaXml()
    {
        var companyId = Guid.NewGuid();
        var invoiceId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        await using var billing = VerifactuSifTestDb.CreateBilling(tenant);
        await using var app = VerifactuSifTestDb.CreateApp(tenant, companyId);

        billing.Invoices.Add(new Invoice
        {
            Id = invoiceId,
            CompanyId = companyId,
            Number = "A-2026-000050",
            Series = "A",
            FiscalYear = 2026,
            SequenceNumber = 50,
            IssueDate = new DateTime(2026, 5, 10, 0, 0, 0, DateTimeKind.Utc),
            DueDate = DateTime.UtcNow,
            Subtotal = 200m,
            TaxAmount = 42m,
            Total = 242m,
            IsLocked = true,
            LockedAt = DateTime.UtcNow,
            VerifactuHuella = "H-50",
            InvoiceLines =
            [
                new InvoiceLine
                {
                    Id = Guid.NewGuid(),
                    Description = "Servicio",
                    Quantity = 1,
                    UnitPrice = 200,
                    TaxRate = 21,
                    TaxAmount = 42,
                    LineTotal = 200,
                },
            ],
        });
        await billing.SaveChangesAsync();

        var exporter = new VerifactuConservationExporter(
            billing,
            app,
            new VerifactuXmlGenerator(
                billing, app, new FakeVerifactuService(),
                Options.Create(new VerifactuOptions
                {
                    NifSoftware = "B12345674",
                    NombreSoftware = "Test ERP",
                    IdSistema = "TEST-1",
                })));

        var package = await exporter.ExportAsync(companyId, 2026, 5, CancellationToken.None);

        Assert.Equal(1, package.InvoiceCount);
        Assert.Contains("Conservacion", package.FileName);

        using var zip = new ZipArchive(new MemoryStream(package.ZipBytes));
        Assert.NotNull(zip.GetEntry("manifest.json"));
        Assert.NotNull(zip.GetEntry("index.json"));
        Assert.NotNull(zip.GetEntry("alta/A-2026-000050.xml"));

        using var manifestReader = new StreamReader(zip.GetEntry("manifest.json")!.Open(), Encoding.UTF8);
        var manifest = await manifestReader.ReadToEndAsync();
        Assert.Contains("RRSIF-Conservacion-v1", manifest);
        Assert.Contains("retentionYears", manifest);
    }
}

public class LockInvoiceRectificativaAnulacionTests
{
    [Fact]
    public async Task LockRectificativa_TriggersAnulacionOfOriginal()
    {
        var companyId = Guid.NewGuid();
        var origId = Guid.NewGuid();
        var rectId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        await using var billing = VerifactuSifTestDb.CreateBilling(tenant);
        await using var app = VerifactuSifTestDb.CreateApp(tenant, companyId);

        billing.Invoices.AddRange(
            new Invoice
            {
                Id = origId,
                CompanyId = companyId,
                Number = "A-2026-000020",
                Series = "A",
                FiscalYear = 2026,
                SequenceNumber = 20,
                InvoiceType = "Normal",
                IssueDate = DateTime.UtcNow.AddDays(-5),
                DueDate = DateTime.UtcNow,
                Subtotal = 100m,
                TaxAmount = 21m,
                Total = 121m,
                IsLocked = true,
                LockedAt = DateTime.UtcNow.AddDays(-5),
                VerifactuHuella = "H-ORIG-20",
            },
            new Invoice
            {
                Id = rectId,
                CompanyId = companyId,
                Number = "R-2026-000002",
                Series = "R",
                FiscalYear = 2026,
                SequenceNumber = 2,
                InvoiceType = "Rectificativa",
                RectifiedInvoiceId = origId,
                IssueDate = DateTime.UtcNow,
                DueDate = DateTime.UtcNow.AddDays(30),
                Subtotal = -100m,
                TaxAmount = -21m,
                Total = -121m,
                Status = "Draft",
                ClientName = "Cliente",
                InvoiceLines =
                [
                    new InvoiceLine
                    {
                        Id = Guid.NewGuid(),
                        Description = "Abono",
                        Quantity = -1,
                        UnitPrice = 100,
                        TaxRate = 21,
                    },
                ],
            });
        await billing.SaveChangesAsync();

        var anulRegistrar = new FakeVerifactuAnulacionRegistrar();
        var handler = new LockInvoiceHandler(
            billing,
            app,
            new FakeVerifactuService(),
            new FakeVerifactuSubmissionService(),
            new FakePublisher(),
            new FakeVerifactuSubmissionGateway(),
            new FakeVerifactuModeSettings { RealtimeSubmissionEnabled = true },
            anulRegistrar,
            new FakeVerifactuChainQuery(),
            new FakeCurrentUserAccessor(),
            new FakeBillingInvoiceSalesLinkQuery(),
            NullLogger<LockInvoiceHandler>.Instance);

        await handler.Handle(new LockInvoiceCommand { Id = rectId }, CancellationToken.None);

        Assert.Contains(origId, anulRegistrar.Registered);
    }
}

// Shared helpers for tests in this file
static class VerifactuSifTestDb
{
    public static BillingDbContext CreateBilling(FakeTenantContext tenant)
    {
        var opts = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase($"vf-sif-billing-{Guid.NewGuid()}")
            .Options;
        return new BillingDbContext(opts, tenant);
    }

    public static ErpDbContext CreateApp(FakeTenantContext tenant, Guid companyId)
    {
        var opts = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"vf-sif-app-{Guid.NewGuid()}")
            .Options;
        var ctx = new ErpDbContext(opts, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Empresa test",
            TaxId = "B12345674",
            Address = "Calle 1",
            SubscriptionId = Guid.NewGuid(),
        });
        ctx.SaveChanges();
        return ctx;
    }
}

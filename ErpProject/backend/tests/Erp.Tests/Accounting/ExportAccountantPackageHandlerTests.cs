using System.IO.Compression;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Accounting.Application.Features.AccountantExport;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class ExportAccountantPackageHandlerTests
{
    [Fact]
    public async Task Handle_BuildsZipWithLibroIvaJournalAndPdfStructure()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"acct-export-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Test SL",
            TaxId = "B12345674",
            IsActive = true,
            Country = "ES",
            AccountantEmail = "gestoria@test.com",
        });
        await ctx.SaveChangesAsync();

        var handler = new ExportAccountantPackageHandler(
            ctx,
            tenant,
            new FakeLibroExporter("emitidas.csv"),
            new FakeLibroExporter("recibidas.csv"),
            new FakeJournalExporter("diario.csv"),
            new FakeBillingPdfExporter(),
            new FakeExpensePdfExporter(),
            new FakeEmailService());

        var result = await handler.Handle(new ExportAccountantPackageCommand(2026, 6, null, false), CancellationToken.None);

        Assert.True(result.ZipContent.Length > 100);
        Assert.Contains("gestoria", result.FileName);
        Assert.Contains("2026-06", result.FileName);
        Assert.False(result.EmailSent);

        using var zip = new ZipArchive(new MemoryStream(result.ZipContent), ZipArchiveMode.Read);
        var names = zip.Entries.Select(e => e.FullName.Replace('\\', '/')).ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Contains("LEEME.txt", names);
        Assert.Contains("libros-iva/emitidas.csv", names);
        Assert.Contains("libros-iva/recibidas.csv", names);
        Assert.Contains("asientos/diario.csv", names);
        Assert.Contains("facturas/FAC-001.pdf", names);

        var readmeEntry = zip.GetEntry("LEEME.txt");
        Assert.NotNull(readmeEntry);
        using var reader = new StreamReader(readmeEntry!.Open(), Encoding.UTF8);
        var readme = await reader.ReadToEndAsync();
        Assert.Contains("asientos/", readme);
        Assert.Contains("facturas/", readme);
    }

    [Fact]
    public async Task Handle_QuarterFilter_UsesQuarterInFileName()
    {
        var companyId = Guid.NewGuid();
        var tenant = new FakeTenantContext();
        tenant.SetTenant(companyId, "Empresa");

        var options = new DbContextOptionsBuilder<ErpDbContext>()
            .UseInMemoryDatabase($"acct-export-q-{Guid.NewGuid()}")
            .Options;
        await using var ctx = new ErpDbContext(options, tenant);
        ctx.Companies.Add(new Company
        {
            Id = companyId,
            Name = "Test SL",
            TaxId = "B12345674",
            IsActive = true,
            Country = "ES",
        });
        await ctx.SaveChangesAsync();

        var handler = new ExportAccountantPackageHandler(
            ctx, tenant,
            new FakeLibroExporter("emitidas.csv"),
            new FakeLibroExporter("recibidas.csv"),
            new FakeJournalExporter("diario.csv"),
            new FakeBillingPdfExporter(),
            new FakeExpensePdfExporter(),
            new FakeEmailService());

        var result = await handler.Handle(new ExportAccountantPackageCommand(2026, null, 2, false), CancellationToken.None);

        Assert.Contains("Q2-2026", result.FileName);
    }

    private sealed class FakeLibroExporter(string fileName) : ILibroIvaEmitidasExporter, ILibroIvaRecibidasExporter
    {
        public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
            => Task.FromResult(new FiscalCsvExportResult
            {
                FileName = fileName,
                Content = Encoding.UTF8.GetBytes("col1;col2\n1;2"),
            });
    }

    private sealed class FakeJournalExporter(string fileName) : IJournalEntriesPeriodExporter
    {
        public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
            => Task.FromResult(new FiscalCsvExportResult
            {
                FileName = fileName,
                Content = Encoding.UTF8.GetBytes("Fecha;Asiento\n01/06/2026;AS-1"),
            });
    }

    private sealed class FakeBillingPdfExporter : IAccountantBillingPdfExporter
    {
        public Task<IReadOnlyList<AccountantZipFile>> ExportLockedInvoicePdfsAsync(
            Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountantZipFile>>([
                new AccountantZipFile("facturas/FAC-001.pdf", [0x25, 0x50, 0x44, 0x46]),
            ]);
    }

    private sealed class FakeExpensePdfExporter : IAccountantExpensePdfExporter
    {
        public Task<IReadOnlyList<AccountantZipFile>> ExportExpensePdfsAsync(
            Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
            => Task.FromResult<IReadOnlyList<AccountantZipFile>>([]);
    }
}

public class AccountantExportJobTests
{
    [Theory]
    [InlineData("monthly", true)]
    [InlineData("quarterly", true)]
    [InlineData("disabled", false)]
    public void ShouldRun_RespectsFrequency(string frequency, bool expected)
    {
        var now = new DateTime(2026, 7, 5, 0, 0, 0, DateTimeKind.Utc);
        var result = Erp.Modules.Accounting.Infrastructure.Jobs.AccountantExportJob.ShouldRun(frequency, now, null);
        Assert.Equal(expected, result);
    }
}

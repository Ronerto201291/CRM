using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Infrastructure.Data;
using Erp.Modules.Accounting.Application.Features.AccountantExport;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Tests.TestSupport;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Erp.Tests.Accounting;

public class ExportAccountantPackageHandlerTests
{
    [Fact]
    public async Task Handle_BuildsZipWithLibroIvaFiles()
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
            new FakeEmailService());

        var result = await handler.Handle(new ExportAccountantPackageCommand(2026, 6, null, false), CancellationToken.None);

        Assert.True(result.ZipContent.Length > 100);
        Assert.Contains("gestoria", result.FileName);
        Assert.False(result.EmailSent);
    }

    private sealed class FakeLibroExporter(string fileName) : ILibroIvaEmitidasExporter, ILibroIvaRecibidasExporter
    {
        public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
            => Task.FromResult(new FiscalCsvExportResult
            {
                FileName = fileName,
                Content = System.Text.Encoding.UTF8.GetBytes("col1;col2\n1;2"),
            });
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

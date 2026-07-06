using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Infrastructure.Services;

namespace Erp.Tests.TestSupport;

public sealed class FakeModelo303Reader : IModelo303Reader
{
    public Task<Modelo303QuarterData> GetQuarterAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
        => Task.FromResult(new Modelo303QuarterData(
            year,
            quarter,
            new DateTime(year, (quarter - 1) * 3 + 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(year, quarter * 3, 1, 0, 0, 0, DateTimeKind.Utc),
            "B12345674",
            "Empresa Test SL",
            [new Modelo303RateLine(21m, 1000m, 210m, "01", "03")],
            [],
            0m,
            0m,
            210m,
            50m,
            160m));
}

public sealed class FakeModelo347Reader : IModelo347Reader
{
    public Task<Modelo347YearData> GetYearAsync(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new Modelo347YearData(
            year,
            "B12345674",
            "Empresa Test SL",
            Modelo347Reader.Threshold347,
            [new Modelo347OperatorRow("12345678Z", "Cliente SA", 5000m, 4132.23m, 867.77m, 0m, 3, false)],
            [new Modelo347OperatorRow("B87654321", "Proveedor SL", 3200m, 2644.63m, 555.37m, 0m, 2, false)]));
}

public sealed class FakeModelo303Exporter : IModelo303Exporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("casilla,valor\n01,1000"),
            FileName = $"Modelo303_{year}_T{quarter}.csv",
            Disclaimer = "Export test",
        });
}

public sealed class FakeVerifactuXmlGenerator : Erp.Application.Common.Interfaces.IVerifactuXmlGenerator
{
    public Task<string> GenerateRegistroAsync(Guid tenantId, int year, int month, CancellationToken ct = default)
        => Task.FromResult("<Verifactu>test</Verifactu>");

    public Task<string> GenerateSingleInvoiceRegistroAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult($"<Verifactu invoice=\"{invoiceId}\" />");

    public Task<string> GenerateAnulacionRegistroAsync(Guid invoiceId, CancellationToken ct = default)
        => Task.FromResult($"<VerifactuAnulacion invoice=\"{invoiceId}\" />");
}

public sealed class FakeModelo111Reader : IModelo111Reader
{
    public Task<Modelo111Result> GetAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
        => Task.FromResult(new Modelo111Result(
            year, quarter, $"T{quarter} {year}", "B12345674", "Empresa Test SL",
            2, 1000m, 150m, 1, 2000m, 300m, 450m,
            [new Modelo111InvoiceLine("A-001", DateTime.UtcNow, "12345678Z", "Profesional", 500m, 15m, 75m)],
            [new Modelo111NominaLine("87654321X", "Empleado", 3, 2000m, 15m, 300m)]));
}

public sealed class FakeModelo390Exporter : IModelo390Exporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("modelo390,test"),
            FileName = $"Modelo390_{year}.csv",
            Disclaimer = "Export 390 test",
        });
}

public sealed class FakeModelo347Exporter : IModelo347Exporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("347,csv"),
            FileName = $"Modelo347_{year}.csv",
            Disclaimer = "347 csv",
        });

    public Task<FiscalCsvExportResult> ExportAeatTxtAsync(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("347,txt"),
            FileName = $"Modelo347_{year}.txt",
            Disclaimer = "347 txt",
        });
}

public sealed class FakeModelo349Exporter : IModelo349Exporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, bool asCsv, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("349,test"),
            FileName = asCsv ? $"Modelo349_{year}_T{quarter}.csv" : $"Modelo349_{year}_T{quarter}.xml",
            Disclaimer = "349 export",
        });
}

public sealed class FakeLibroIvaRecibidasExporter : ILibroIvaRecibidasExporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("libro,recibidas"),
            FileName = $"LibroIvaRecibidas_{period.FileSuffix}.csv",
            Disclaimer = "Libro IVA recibidas",
        });
}

public sealed class FakeLibroIvaEmitidasExporter : ILibroIvaEmitidasExporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("libro,emitidas"),
            FileName = $"LibroIvaEmitidas_{period.FileSuffix}.csv",
            Disclaimer = "Libro IVA emitidas",
        });
}

public sealed class FakeModelo303XmlExporter : IModelo303XmlExporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("<Modelo303/>"),
            FileName = $"Modelo303_{year}_T{quarter}.xml",
            Disclaimer = "303 xml",
        });
}

public sealed class FakeFiscalSkeletonXmlExporter : IFiscalSkeletonXmlExporter
{
    public Task<FiscalCsvExportResult> ExportModelo200Async(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("<Modelo200/>"),
            FileName = $"Modelo200_{year}.xml",
            Disclaimer = "200 xml",
        });

    public Task<FiscalCsvExportResult> ExportModelo202Async(Guid tenantId, int year, int period, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("<Modelo202/>"),
            FileName = $"Modelo202_{year}_P{period}.xml",
            Disclaimer = "202 xml",
        });
}

public sealed class FakeModelo390XmlExporter : IModelo390XmlExporter
{
    public Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
        => Task.FromResult(new FiscalCsvExportResult
        {
            Content = System.Text.Encoding.UTF8.GetBytes("<Modelo390/>"),
            FileName = $"Modelo390_{year}.xml",
            Disclaimer = "390 xml",
        });
}

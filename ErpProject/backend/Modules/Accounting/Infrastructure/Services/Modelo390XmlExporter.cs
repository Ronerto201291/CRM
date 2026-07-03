using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo390XmlExporter : IModelo390XmlExporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IAccountingDbContext _accounting;
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;

    public Modelo390XmlExporter(
        IAccountingDbContext accounting,
        IBillingDbContext billing,
        IApplicationDbContext app)
    {
        _accounting = accounting;
        _billing = billing;
        _app = app;
    }

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);

        var yearStart = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var yearEnd = new DateTime(year + 1, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var allYearInvoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= yearStart && i.IssueDate < yearEnd)
            .AsNoTracking().ToListAsync(ct);

        var allYearLines = allYearInvoices.SelectMany(i => i.InvoiceLines).ToList();
        var yearByRate = allYearLines.Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.TaxAmount), 2)));
        var yearByRecargo = allYearLines.Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.SurchargeAmount), 2)));

        var yearIntracom = Math.Round(allYearLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var yearExportac = Math.Round(allYearLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        var yearDeducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date.Year == year)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        var totalDevengadoAnual = Math.Round(yearByRate.Values.Sum(v => v.Cuota) + yearByRecargo.Values.Sum(v => v.Cuota), 2);
        var resultadoAnual = totalDevengadoAnual - yearDeducible;
        string F(decimal d) => d.ToString("F2", Es);

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T390",
                new XComment($"Modelo 390 Resumen Anual IVA — {year} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                new XElement("Ejercicio", year.ToString()),
                new XElement("NIF", company?.TaxId ?? string.Empty),
                new XElement("ApellidosNombre", company?.Name ?? string.Empty),
                new XElement("TipoDeclaracion", "N"),
                new XElement("Pagina1",
                    new XElement("Casilla001", F(yearByRate.TryGetValue(21m, out var b21) ? b21.Base : 0m)),
                    new XElement("Casilla002", F(yearByRate.TryGetValue(21m, out var c21) ? c21.Cuota : 0m)),
                    new XElement("Casilla003", F(yearByRate.TryGetValue(10m, out var b10) ? b10.Base : 0m)),
                    new XElement("Casilla004", F(yearByRate.TryGetValue(10m, out var c10) ? c10.Cuota : 0m)),
                    new XElement("Casilla005", F(yearByRate.TryGetValue(4m, out var b4) ? b4.Base : 0m)),
                    new XElement("Casilla006", F(yearByRate.TryGetValue(4m, out var c4) ? c4.Cuota : 0m)),
                    new XElement("Casilla007", F(yearByRate.TryGetValue(0m, out var b0) ? b0.Base : 0m)),
                    new XElement("Casilla008", F(yearByRate.TryGetValue(0m, out var c0) ? c0.Cuota : 0m)),
                    new XElement("Casilla031", F(yearByRecargo.TryGetValue(5.2m, out var br52) ? br52.Base : 0m)),
                    new XElement("Casilla032", F(yearByRecargo.TryGetValue(5.2m, out var cr52) ? cr52.Cuota : 0m)),
                    new XElement("Casilla033", F(yearByRecargo.TryGetValue(1.4m, out var br14) ? br14.Base : 0m)),
                    new XElement("Casilla034", F(yearByRecargo.TryGetValue(1.4m, out var cr14) ? cr14.Cuota : 0m)),
                    new XElement("Casilla035", F(yearByRecargo.TryGetValue(0.5m, out var br05) ? br05.Base : 0m)),
                    new XElement("Casilla036", F(yearByRecargo.TryGetValue(0.5m, out var cr05) ? cr05.Cuota : 0m)),
                    new XElement("Casilla059", F(yearIntracom)),
                    new XElement("Casilla060", F(yearExportac)),
                    new XElement("Casilla027", F(totalDevengadoAnual))),
                new XElement("Pagina2",
                    new XElement("Casilla028", F(yearDeducible)),
                    new XElement("Casilla029", F(yearDeducible)),
                    new XElement("Casilla045", F(yearDeducible))),
                new XElement("Pagina3",
                    new XElement("Casilla051", "0.00"),
                    new XElement("Casilla052", "0.00"),
                    new XElement("Casilla065", F(Math.Abs(resultadoAnual))),
                    new XElement("TipoResultado", resultadoAnual >= 0 ? "AIngresar" : "ADevolver"),
                    new XElement("Casilla046", F(Math.Abs(resultadoAnual))))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        return new FiscalCsvExportResult
        {
            Content = xmlBytes,
            ContentType = "application/xml",
            FileName = $"Modelo390_{year}.xml",
            Disclaimer = "XML modelo 390: contrastar XSD y diseño de registro AEAT vigente antes de presentar."
        };
    }
}

using System.Globalization;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo390Exporter : IModelo390Exporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IAccountingDbContext _accounting;
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;

    public Modelo390Exporter(
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

        var allLines = allYearInvoices.SelectMany(i => i.InvoiceLines).ToList();
        var yearByRate = allLines.Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.TaxAmount), 2)));
        var yearByRecargo = allLines.Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key,
                g => (Base: Math.Round(g.Sum(l => l.LineTotal), 2),
                      Cuota: Math.Round(g.Sum(l => l.SurchargeAmount), 2)));

        var yearIntracom = Math.Round(allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var yearExportac = Math.Round(allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        var yearDeducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date.Year == year)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        var totalDevengado = Math.Round(yearByRate.Values.Sum(v => v.Cuota) + yearByRecargo.Values.Sum(v => v.Cuota), 2);
        var resultado = totalDevengado - yearDeducible;

        var sb = new StringBuilder();
        sb.AppendLine($"MODELO 390 — Resumen Anual IVA. Ejercicio {year}");
        sb.AppendLine($"NIF;{company?.TaxId}"); sb.AppendLine($"Razón Social;{company?.Name}");

        sb.AppendLine(); sb.AppendLine("IVA DEVENGADO — NACIONAL");
        sb.AppendLine("Tipo;BaseImponible;CuotaIVA");
        foreach (var (rate, vals) in yearByRate.OrderByDescending(r => r.Key))
            sb.AppendLine($"Nacional {rate:F0}%;{vals.Base.ToString("F2", Es)};{vals.Cuota.ToString("F2", Es)}");

        if (yearByRecargo.Count > 0)
        {
            sb.AppendLine(); sb.AppendLine("RECARGO DE EQUIVALENCIA");
            foreach (var (rate, vals) in yearByRecargo.OrderByDescending(r => r.Key))
                sb.AppendLine($"Recargo {rate:F1}%;{vals.Base.ToString("F2", Es)};{vals.Cuota.ToString("F2", Es)}");
        }

        if (yearIntracom > 0 || yearExportac > 0)
        {
            sb.AppendLine(); sb.AppendLine("EXENTAS");
            if (yearIntracom > 0) sb.AppendLine($"Intracomunitarias (casilla 59);{yearIntracom.ToString("F2", Es)}");
            if (yearExportac > 0) sb.AppendLine($"Exportaciones (casilla 60);{yearExportac.ToString("F2", Es)}");
        }

        sb.AppendLine(); sb.AppendLine($"Total IVA Devengado (casilla 27);{totalDevengado.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine($"IVA Deducible (casilla 45);{yearDeducible.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine($"RESULTADO ({(resultado >= 0 ? "A INGRESAR" : "A DEVOLVER")}) (casilla 65);{Math.Abs(resultado).ToString("F2", Es)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            ContentType = "text/csv",
            FileName = $"Modelo390_{year}.csv",
            Disclaimer = "CSV resumen 390 orientativo; validar contra modelo oficial AEAT."
        };
    }
}

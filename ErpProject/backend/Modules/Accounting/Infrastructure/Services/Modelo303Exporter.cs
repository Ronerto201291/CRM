using System.Globalization;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Infrastructure.Services;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo303Exporter : IModelo303Exporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IAccountingDbContext _accounting;
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;

    public Modelo303Exporter(
        IAccountingDbContext accounting,
        IBillingDbContext billing,
        IApplicationDbContext app)
    {
        _accounting = accounting;
        _billing = billing;
        _app = app;
    }

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking().FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = FiscalQuarterHelper.QuarterRange(year, quarter);

        var ivaDeducible = await _accounting.JournalEntryLines
            .Include(l => l.Account).Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == tenantId && l.Account.Code.StartsWith("472")
                && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
            .AsNoTracking().ToListAsync(ct);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var allLines = invoices.SelectMany(i => i.InvoiceLines).ToList();
        var nacionales = allLines.Where(l => l.TipoOperacion == "Nacional");
        var byRate = nacionales.GroupBy(l => l.TaxRate)
            .ToDictionary(g => g.Key, g => new { Base = g.Sum(l => l.LineTotal), Cuota = g.Sum(l => l.TaxAmount) });
        var bySurchargeRate = nacionales.Where(l => l.SurchargeRate > 0).GroupBy(l => l.SurchargeRate)
            .ToDictionary(g => g.Key, g => new { Base = g.Sum(l => l.LineTotal), Cuota = g.Sum(l => l.SurchargeAmount) });

        var intracom = allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal);
        var exportaciones = allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal);

        decimal totalIVADevengado = Math.Round(byRate.Values.Sum(v => v.Cuota), 2);
        decimal totalRecargoDevengado = Math.Round(bySurchargeRate.Values.Sum(v => v.Cuota), 2);
        decimal totalDevengado = totalIVADevengado + totalRecargoDevengado;
        decimal totalDeducible = Math.Round(ivaDeducible.Sum(l => l.Debit), 2);
        decimal resultado = totalDevengado - totalDeducible;

        var sb = new StringBuilder();
        sb.AppendLine($"MODELO 303 — IVA. Declaración trimestral {year} T{quarter}");
        sb.AppendLine($"NIF;{company?.TaxId}");
        sb.AppendLine($"Razón Social;{company?.Name}");
        sb.AppendLine($"Período;{year}-T{quarter} ({from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy})");

        sb.AppendLine(); sb.AppendLine("DEVENGADO — IVA NACIONAL");
        sb.AppendLine("Tipo;BaseImponible;Casilla_Base;Cuota;Casilla_Cuota");
        foreach (var (rate, vals) in byRate.OrderByDescending(r => r.Key))
        {
            var (casBase, casCuota) = FiscalQuarterHelper.RateToCasillas(rate);
            sb.AppendLine(string.Join(";", $"Nacional {rate:F0}%",
                vals.Base.ToString("F2", Es), casBase, vals.Cuota.ToString("F2", Es), casCuota));
        }

        if (bySurchargeRate.Count > 0)
        {
            sb.AppendLine(); sb.AppendLine("RECARGO DE EQUIVALENCIA");
            sb.AppendLine("Tipo;BaseImponible;Casilla_Base;Cuota;Casilla_Cuota");
            foreach (var (rate, vals) in bySurchargeRate.OrderByDescending(r => r.Key))
            {
                var (casBase, casCuota) = FiscalQuarterHelper.SurchargeRateToCasillas(rate);
                sb.AppendLine(string.Join(";", $"Recargo {rate:F1}%",
                    vals.Base.ToString("F2", Es), casBase, vals.Cuota.ToString("F2", Es), casCuota));
            }
        }

        if (intracom > 0 || exportaciones > 0)
        {
            sb.AppendLine(); sb.AppendLine("EXENTAS");
            if (intracom > 0)
                sb.AppendLine($"Entregas intracomunitarias (casilla 59);{intracom.ToString("F2", Es)}");
            if (exportaciones > 0)
                sb.AppendLine($"Exportaciones (casilla 60);{exportaciones.ToString("F2", Es)}");
        }

        sb.AppendLine(); sb.AppendLine($"Total IVA Devengado (casilla 27);{totalDevengado.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine("DEDUCIBLE");
        sb.AppendLine($"IVA Soportado corriente (casilla 28);{totalDeducible.ToString("F2", Es)}");
        sb.AppendLine(); sb.AppendLine("LIQUIDACIÓN");
        sb.AppendLine($"Resultado ({(resultado >= 0 ? "A INGRESAR" : "A COMPENSAR")}) (casilla 46);{Math.Abs(resultado).ToString("F2", Es)}");

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"Modelo303_{year}_T{quarter}.csv",
            Disclaimer = "CSV modelo 303 orientativo; validar con el modelo oficial y plataforma AEAT.",
        };
    }
}

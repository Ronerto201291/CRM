using System.Globalization;
using System.Text;
using System.Xml.Linq;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo349Exporter : IModelo349Exporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;
    private readonly IApplicationDbContext _app;

    public Modelo349Exporter(
        IBillingDbContext billing,
        IExpensesDbContext expenses,
        IApplicationDbContext app)
    {
        _billing = billing;
        _expenses = expenses;
        _app = app;
    }

    public async Task<FiscalCsvExportResult> ExportAsync(
        Guid tenantId, int year, int quarter, bool asCsv, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = FiscalQuarterHelper.QuarterRange(year, quarter);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var entregasRows = invoices
            .Where(i => i.InvoiceLines.Any(l => l.TipoOperacion == "IntraComunitario"))
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new Modelo349Row(
                EuVatHelper.GetCountryCode(g.Key.ClientNif),
                EuVatHelper.StripCountryPrefix(g.Key.ClientNif),
                g.Key.ClientName ?? string.Empty,
                "E",
                Math.Round(g.Sum(i =>
                    i.InvoiceLines.Where(l => l.TipoOperacion == "IntraComunitario")
                                  .Sum(l => l.LineTotal)), 2),
                g.Count()))
            .Where(r => r.BaseImponible > 0)
            .ToList();

        var expenseDocs = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId
                     && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to
                     && e.SupplierTaxId != null)
            .Select(e => new { e.SupplierTaxId, e.SupplierName, e.Total })
            .AsNoTracking().ToListAsync(ct);

        var adquisRows = expenseDocs
            .GroupBy(e => new { e.SupplierTaxId, e.SupplierName })
            .Select(g => new Modelo349Row(
                EuVatHelper.GetCountryCode(g.Key.SupplierTaxId),
                EuVatHelper.StripCountryPrefix(g.Key.SupplierTaxId),
                g.Key.SupplierName ?? string.Empty,
                "A",
                Math.Round(g.Sum(e => (decimal?)e.Total ?? 0m), 2),
                g.Count()))
            .Where(r => r.CountryCode != "ES" && r.CountryCode != "ZZ" && r.BaseImponible > 0)
            .ToList();

        var allRows = entregasRows.Concat(adquisRows)
            .OrderByDescending(r => r.BaseImponible)
            .ToList();

        var periodo = quarter switch { 1 => "1T", 2 => "2T", 3 => "3T", _ => "4T" };
        var totalOps = allRows.Sum(r => r.NumOperaciones);
        var totalBase = Math.Round(allRows.Sum(r => r.BaseImponible), 2);

        if (asCsv)
        {
            var sb = new StringBuilder();
            sb.AppendLine($"MODELO 349 — Operaciones intracomunitarias. {year} {periodo}");
            sb.AppendLine($"NIF declarante;{company?.TaxId}");
            sb.AppendLine($"Razón Social;{company?.Name}");
            sb.AppendLine($"Total operaciones;{totalOps}  Total base;{totalBase.ToString("F2", Es)}");
            sb.AppendLine();
            sb.AppendLine("PaisOp;NIF_Op;NombreOp;ClaveOp;BaseImponible;NumOps");
            foreach (var r in allRows)
                sb.AppendLine(string.Join(";",
                    r.CountryCode, r.NifOp ?? string.Empty,
                    r.NombreOp.Replace(";", " "),
                    r.ClaveOp, r.BaseImponible.ToString("F2", Es),
                    r.NumOperaciones.ToString()));
            var csvBytes = Encoding.UTF8.GetPreamble()
                .Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
            return new FiscalCsvExportResult
            {
                Content = csvBytes,
                ContentType = "text/csv",
                FileName = $"Modelo349_{year}_{periodo}.csv",
                Disclaimer = "CSV modelo 349 orientativo; validar con diseño AEAT y asesoría."
            };
        }

        var xml = new XDocument(
            new XDeclaration("1.0", "UTF-8", null),
            new XElement("T349",
                new XComment($"Modelo 349 — {year} {periodo} — Generado {DateTime.UtcNow:yyyy-MM-ddTHH:mm:ssZ}"),
                new XElement("Declarante",
                    new XElement("NIF", company?.TaxId ?? string.Empty),
                    new XElement("NombreRazon", company?.Name ?? string.Empty),
                    new XElement("Ejercicio", year.ToString()),
                    new XElement("Periodo", periodo),
                    new XElement("Periodicidad", totalBase > 50000m ? "M" : "T"),
                    new XElement("TipoDeclaracion", "N"),
                    new XElement("NombreContacto", company?.Name ?? string.Empty),
                    new XElement("NumOperaciones", totalOps.ToString()),
                    new XElement("ImporteTotalOper", totalBase.ToString("F2", Es))),
                new XElement("Operadores",
                    allRows.Select(r =>
                        new XElement("Operador",
                            new XElement("CodigoPaisOp", r.CountryCode.ToUpperInvariant()),
                            new XElement("IdOperador", r.NifOp ?? string.Empty),
                            new XElement("NombreOp", r.NombreOp.Length > 40 ? r.NombreOp[..40] : r.NombreOp),
                            new XElement("ClaveOp", r.ClaveOp),
                            new XElement("BaseImponible", r.BaseImponible.ToString("F2", Es)))))));

        var xmlBytes = Encoding.UTF8.GetBytes(xml.Declaration + "\n" + xml);
        return new FiscalCsvExportResult
        {
            Content = xmlBytes,
            ContentType = "application/xml",
            FileName = $"Modelo349_{year}_{periodo}.xml",
            Disclaimer = "XML modelo 349: contrastar especificación AEAT vigente antes de presentar."
        };
    }

    private sealed record Modelo349Row(
        string CountryCode, string? NifOp, string NombreOp,
        string ClaveOp, decimal BaseImponible, int NumOperaciones);
}

using System.Globalization;
using System.Text;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public sealed class JournalEntriesPeriodExporter(IAccountingDbContext accounting) : IJournalEntriesPeriodExporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, FiscalExportPeriod period, CancellationToken ct)
    {
        var from = period.FromUtc;
        var to = period.ToUtc;

        var entries = await accounting.JournalEntries
            .Include(e => e.JournalEntryLines).ThenInclude(l => l.Account)
            .Where(e => e.CompanyId == tenantId && e.IsPosted && e.Date >= from && e.Date < to)
            .OrderBy(e => e.Date).ThenBy(e => e.Reference)
            .AsNoTracking()
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha;Asiento;Cuenta;NombreCuenta;Concepto;Debe;Haber");
        foreach (var entry in entries)
        {
            foreach (var line in entry.JournalEntryLines.OrderBy(l => l.Account?.Code))
            {
                sb.AppendLine(string.Join(";",
                    entry.Date.ToString("dd/MM/yyyy", Es),
                    entry.Reference ?? entry.Id.ToString()[..8],
                    line.Account?.Code ?? string.Empty,
                    (line.Account?.Name ?? string.Empty).Replace(";", " "),
                    (entry.Description ?? string.Empty).Replace(";", " "),
                    line.Debit.ToString("F2", Es),
                    line.Credit.ToString("F2", Es)));
            }
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"LibroDiario_{period.FileSuffix}.csv",
            Disclaimer = "CSV libro diario interno; no sustituye el libro oficial del Código de Comercio sin asesoría.",
        };
    }
}

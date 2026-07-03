using System.Globalization;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.Export;

public record ExportLibroDiarioQuery(int Year) : IRequest<FiscalCsvExportResult>;

public class FiscalCsvExportResult
{
    public byte[] Content { get; init; } = [];
    public string ContentType { get; init; } = "text/csv";
    public string FileName { get; init; } = string.Empty;
    public string Disclaimer { get; init; } = string.Empty;
}

public class ExportLibroDiarioHandler : IRequestHandler<ExportLibroDiarioQuery, FiscalCsvExportResult>
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IAccountingDbContext _accounting;
    private readonly ITenantContext _tenant;

    public ExportLibroDiarioHandler(IAccountingDbContext accounting, ITenantContext tenant)
    {
        _accounting = accounting;
        _tenant = tenant;
    }

    public async Task<FiscalCsvExportResult> Handle(ExportLibroDiarioQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var entries = await _accounting.JournalEntries
            .Include(e => e.JournalEntryLines).ThenInclude(l => l.Account)
            .Where(e => e.CompanyId == tenantId && e.Date.Year == request.Year)
            .OrderBy(e => e.Date)
            .AsNoTracking()
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Fecha;Asiento;Cuenta;NombreCuenta;Concepto;Debe;Haber");
        foreach (var entry in entries)
        {
            foreach (var line in entry.JournalEntryLines.OrderBy(l => l.Account?.Code))
            {
                sb.AppendLine(string.Join(";",
                    entry.Date.ToString("dd/MM/yyyy"),
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
            FileName = $"LibroDiario_{request.Year}.csv",
            Disclaimer = "CSV libro diario interno; no sustituye el libro oficial del Código de Comercio sin asesoría.",
        };
    }
}

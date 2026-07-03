using System.Globalization;
using System.Text;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class LibroIvaRecibidasExporter : ILibroIvaRecibidasExporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IExpensesDbContext _expenses;

    public LibroIvaRecibidasExporter(IExpensesDbContext expenses) => _expenses = expenses;

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var docs = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to)
            .OrderBy(e => e.IssueDate)
            .AsNoTracking()
            .ToListAsync(ct);

        var sb = new StringBuilder();
        sb.AppendLine("Libro facturas recibidas;RIVA Art.64;Ejercicio;" + year);
        sb.AppendLine("FechaExpedicion;FechaRegistro;NumeroDocumento;NIFProveedor;NombreProveedor;BaseImponible;CuotaIVASoportada;IRPF;Total;Deducible");
        foreach (var e in docs)
        {
            var fd = e.IssueDate ?? DateTime.UtcNow;
            var baseImp = e.TaxBase ?? e.Total ?? 0m;
            var irpf = e.IRPFAmount ?? 0m;
            sb.AppendLine(string.Join(";",
                fd.ToString("dd/MM/yyyy", Es),
                fd.ToString("dd/MM/yyyy", Es),
                (e.InvoiceNumber ?? e.Id.ToString()[..8]).Replace(";", " "),
                e.SupplierTaxId ?? "",
                (e.SupplierName ?? "").Replace(";", " "),
                baseImp.ToString("F2", Es),
                (e.VATAmount ?? 0m).ToString("F2", Es),
                irpf.ToString("F2", Es),
                ((e.Total ?? 0m) + (e.VATAmount ?? 0m)).ToString("F2", Es),
                (e.VATAmount ?? 0m) > 0 ? "S" : "N"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"LibroIVA_Recibidas_{year}.csv",
            Disclaimer = "CSV libro IVA recibidas orientativo; contrastar con normativa RIVA vigente.",
        };
    }
}

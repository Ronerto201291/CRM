using System.Globalization;
using System.Text;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class LibroIvaEmitidasExporter : ILibroIvaEmitidasExporter
{
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IBillingDbContext _billing;

    public LibroIvaEmitidasExporter(IBillingDbContext billing) => _billing = billing;

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var list = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IssueDate >= from && i.IssueDate < to)
            .OrderBy(i => i.IssueDate).ThenBy(i => i.Number)
            .AsNoTracking()
            .ToListAsync(ct);

        var rectIds = list.Where(i => i.RectifiedInvoiceId.HasValue)
            .Select(i => i.RectifiedInvoiceId!.Value).Distinct().ToList();
        var rectMap = rectIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await _billing.Invoices.AsNoTracking()
                .Where(x => rectIds.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, x => x.Number, ct);

        var sb = new StringBuilder();
        sb.AppendLine("Libro facturas emitidas;RIVA Art.63 campos principales;Ejercicio;" + year);
        sb.AppendLine("FechaExpedicion;NumeroFactura;Serie;NIFCliente;NombreCliente;TipoDoc;RectificaNumero;CausaRectificacion;BaseImponibleTotal;CuotaIVATotal;CuotaRE;RetencionIRPF;ImporteTotal;Exenta;Intracomunitaria;Exportacion");
        foreach (var i in list)
        {
            var intra = i.InvoiceLines.Any(l => l.TipoOperacion == "IntraComunitario");
            var exp = i.InvoiceLines.Any(l => l.TipoOperacion == "Exportacion");
            var rectNum = i.RectifiedInvoiceId.HasValue && rectMap.TryGetValue(i.RectifiedInvoiceId.Value, out var rn)
                ? rn
                : string.Empty;
            sb.AppendLine(string.Join(";",
                i.IssueDate.ToString("dd/MM/yyyy", Es),
                i.Number,
                i.Series,
                i.ClientNif ?? "",
                (i.ClientName ?? "").Replace(";", " "),
                i.InvoiceType,
                rectNum ?? "",
                i.RectificationReasonCode ?? "",
                i.Subtotal.ToString("F2", Es),
                i.TaxAmount.ToString("F2", Es),
                i.SurchargeAmount.ToString("F2", Es),
                i.IrpfAmount.ToString("F2", Es),
                i.Total.ToString("F2", Es),
                i.TaxAmount == 0 && !intra ? "S" : "N",
                intra ? "S" : "N",
                exp ? "S" : "N"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"LibroIVA_Emitidas_{year}.csv",
            Disclaimer = "CSV libro IVA emitidas orientativo; contrastar con normativa RIVA vigente.",
        };
    }
}

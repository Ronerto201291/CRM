using System.Globalization;
using System.Text;
using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo347Exporter : IModelo347Exporter
{
    private const decimal Threshold347 = 3005.06m;
    private static readonly CultureInfo Es = CultureInfo.InvariantCulture;

    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;
    private readonly IApplicationDbContext _app;

    public Modelo347Exporter(
        IBillingDbContext billing,
        IExpensesDbContext expenses,
        IApplicationDbContext app)
    {
        _billing = billing;
        _expenses = expenses;
        _app = app;
    }

    public async Task<FiscalCsvExportResult> ExportAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var invoiceRows = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to
                     && !string.IsNullOrWhiteSpace(i.ClientNif))
            .AsNoTracking()
            .ToListAsync(ct);

        var ventasPorNif = invoiceRows
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new
            {
                Nif = g.Key.ClientNif,
                Nombre = g.Key.ClientName ?? string.Empty,
                ImporteTotal = Math.Round(g.Sum(i =>
                    i.InvoiceLines.Sum(l => l.LineTotal + l.TaxAmount + l.SurchargeAmount)), 2),
                BaseImponible = Math.Round(g.Sum(i => i.Subtotal), 2),
                CuotaIVA = Math.Round(g.Sum(i => i.TaxAmount + i.SurchargeAmount), 2),
                CuotaIRPF = Math.Round(g.Sum(i => i.IrpfAmount), 2),
                NumOperaciones = g.Count(),
                EsPersonaFisica = EsPersonaFisica(g.Key.ClientNif ?? string.Empty)
            })
            .Where(r => r.ImporteTotal >= Threshold347)
            .Where(r => !string.Equals(r.Nif?.Trim(), company?.TaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        var compraRows = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to
                     && !string.IsNullOrWhiteSpace(e.SupplierTaxId))
            .AsNoTracking()
            .ToListAsync(ct);

        var comprasPorNif = compraRows
            .GroupBy(e => new { e.SupplierTaxId, e.SupplierName })
            .Select(g => new
            {
                Nif = g.Key.SupplierTaxId,
                Nombre = g.Key.SupplierName ?? string.Empty,
                ImporteTotal = Math.Round(g.Sum(e => (decimal?)((e.VATAmount ?? 0m) > 0 ? (e.Total ?? 0m) + (e.VATAmount ?? 0m) : (e.Total ?? 0m)) ?? 0m), 2),
                BaseImponible = Math.Round(g.Sum(e => (decimal?)e.Total ?? 0m), 2),
                CuotaIVA = Math.Round(g.Sum(e => (decimal?)e.VATAmount ?? 0m), 2),
                CuotaIRPF = 0m,
                NumOperaciones = g.Count(),
                EsPersonaFisica = EsPersonaFisica(g.Key.SupplierTaxId ?? string.Empty)
            })
            .Where(r => r.ImporteTotal >= Threshold347)
            .Where(r => !string.Equals(r.Nif?.Trim(), company?.TaxId?.Trim(), StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        var sb = new StringBuilder();
        sb.AppendLine("AVISO;Este CSV no sustituye la presentación oficial del modelo 347 sin validación en programa de ayuda AEAT o asesoría.");
        sb.AppendLine($"MODELO 347 — Operaciones con terceros. Año {year}");
        sb.AppendLine($"NIF Declarante;{company?.TaxId}");
        sb.AppendLine($"Razón Social;{company?.Name}");
        sb.AppendLine($"Umbral aplicado;3.005,06 € (IVA incluido)");
        sb.AppendLine($"Nº operadores clientes;{ventasPorNif.Count}");
        sb.AppendLine($"Nº operadores proveedores;{comprasPorNif.Count}");
        sb.AppendLine();
        sb.AppendLine("# CLIENTES (ventas emitidas ≥ 3.005,06 € IVA incluido)");
        sb.AppendLine("NIF;Nombre;ImporteTotal;BaseImponible;CuotaIVA;CuotaIRPF;NumOperaciones;TipoNIF");
        foreach (var r in ventasPorNif)
        {
            sb.AppendLine(string.Join(";",
                r.Nif ?? string.Empty,
                r.Nombre.Replace(";", " "),
                r.ImporteTotal.ToString("F2", Es),
                r.BaseImponible.ToString("F2", Es),
                r.CuotaIVA.ToString("F2", Es),
                r.CuotaIRPF.ToString("F2", Es),
                r.NumOperaciones.ToString(),
                r.EsPersonaFisica ? "F" : "J"));
        }

        sb.AppendLine();
        sb.AppendLine("# PROVEEDORES (compras ≥ 3.005,06 € IVA incluido)");
        sb.AppendLine("NIF;Nombre;ImporteTotal;BaseImponible;CuotaIVA;CuotaIRPF;NumOperaciones;TipoNIF");
        foreach (var r in comprasPorNif)
        {
            sb.AppendLine(string.Join(";",
                r.Nif ?? string.Empty,
                r.Nombre.Replace(";", " "),
                r.ImporteTotal.ToString("F2", Es),
                r.BaseImponible.ToString("F2", Es),
                r.CuotaIVA.ToString("F2", Es),
                r.CuotaIRPF.ToString("F2", Es),
                r.NumOperaciones.ToString(),
                r.EsPersonaFisica ? "F" : "J"));
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return new FiscalCsvExportResult
        {
            Content = bytes,
            FileName = $"Modelo347_{year}.csv",
            Disclaimer = "Modelo 347 CSV resumen interno; no es el fichero físico del diseño de registro AEAT.",
        };
    }

    public async Task<FiscalCsvExportResult> ExportAeatTxtAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var csv = await ExportAsync(tenantId, year, ct);
        var txt = Encoding.UTF8.GetString(csv.Content.Skip(3).ToArray());
        if (txt.StartsWith('\uFEFF')) txt = txt.TrimStart('\uFEFF');
        var body = "# Modelo 347 — texto plano (ORIENTATIVO)\r\n" +
                   "# NO es el registro físico del diseño de registro AEAT hasta contrastarlo con el programa de ayuda oficial.\r\n" +
                   txt.Replace("\n", "\r\n");
        var bytes = Encoding.UTF8.GetBytes(body);
        return new FiscalCsvExportResult
        {
            Content = bytes,
            ContentType = "text/plain",
            FileName = $"Modelo347_{year}_aeat.txt",
            Disclaimer = "TXT 347 orientativo; no sustituye fichero validado por la AEAT.",
        };
    }

    private static bool EsPersonaFisica(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif)) return false;
        if (nif.Length < 9) return false;
        var numPart = nif[..8];
        return numPart.All(char.IsDigit) && !char.IsLetter(nif[0]);
    }
}

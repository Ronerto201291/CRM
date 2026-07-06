using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Expenses.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo347Reader : IModelo347Reader
{
    public const decimal Threshold347 = 3005.06m;

    private readonly IBillingDbContext _billing;
    private readonly IExpensesDbContext _expenses;
    private readonly IApplicationDbContext _app;

    public Modelo347Reader(
        IBillingDbContext billing,
        IExpensesDbContext expenses,
        IApplicationDbContext app)
    {
        _billing = billing;
        _expenses = expenses;
        _app = app;
    }

    public async Task<Modelo347YearData> GetYearAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);
        var companyTaxId = company?.TaxId?.Trim();

        var invoiceRows = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to
                     && !string.IsNullOrWhiteSpace(i.ClientNif))
            .AsNoTracking()
            .ToListAsync(ct);

        var clientes = invoiceRows
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => ToRow(
                g.Key.ClientNif ?? string.Empty,
                g.Key.ClientName ?? string.Empty,
                Math.Round(g.Sum(i => i.InvoiceLines.Sum(l => l.LineTotal + l.TaxAmount + l.SurchargeAmount)), 2),
                Math.Round(g.Sum(i => i.Subtotal), 2),
                Math.Round(g.Sum(i => i.TaxAmount + i.SurchargeAmount), 2),
                Math.Round(g.Sum(i => i.IrpfAmount), 2),
                g.Count()))
            .Where(r => r.ImporteTotal >= Threshold347)
            .Where(r => !string.Equals(r.Nif.Trim(), companyTaxId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        var compraRows = await _expenses.ExpenseDocuments
            .Where(e => e.CompanyId == tenantId && e.Status == "Approved"
                     && e.IssueDate >= from && e.IssueDate < to
                     && !string.IsNullOrWhiteSpace(e.SupplierTaxId))
            .AsNoTracking()
            .ToListAsync(ct);

        var proveedores = compraRows
            .GroupBy(e => new { e.SupplierTaxId, e.SupplierName })
            .Select(g => ToRow(
                g.Key.SupplierTaxId ?? string.Empty,
                g.Key.SupplierName ?? string.Empty,
                Math.Round(g.Sum(e => (decimal?)((e.VATAmount ?? 0m) > 0 ? (e.Total ?? 0m) + (e.VATAmount ?? 0m) : (e.Total ?? 0m)) ?? 0m), 2),
                Math.Round(g.Sum(e => (decimal?)e.Total ?? 0m), 2),
                Math.Round(g.Sum(e => (decimal?)e.VATAmount ?? 0m), 2),
                0m,
                g.Count()))
            .Where(r => r.ImporteTotal >= Threshold347)
            .Where(r => !string.Equals(r.Nif.Trim(), companyTaxId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(r => r.ImporteTotal)
            .ToList();

        return new Modelo347YearData(
            year,
            company?.TaxId,
            company?.Name,
            Threshold347,
            clientes,
            proveedores);
    }

    private static Modelo347OperatorRow ToRow(
        string nif, string nombre, decimal importeTotal, decimal baseImponible,
        decimal cuotaIva, decimal cuotaIrpf, int numOperaciones)
        => new(
            nif,
            nombre,
            importeTotal,
            baseImponible,
            cuotaIva,
            cuotaIrpf,
            numOperaciones,
            EsPersonaFisica(nif));

    public static bool EsPersonaFisica(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif) || nif.Length < 9) return false;
        var numPart = nif[..8];
        return numPart.All(char.IsDigit) && !char.IsLetter(nif[0]);
    }
}

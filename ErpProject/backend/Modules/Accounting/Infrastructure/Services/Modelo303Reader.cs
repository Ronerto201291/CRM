using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo303Reader : IModelo303Reader
{
    private readonly IAccountingDbContext _accounting;
    private readonly IBillingDbContext _billing;
    private readonly IApplicationDbContext _app;

    public Modelo303Reader(
        IAccountingDbContext accounting,
        IBillingDbContext billing,
        IApplicationDbContext app)
    {
        _accounting = accounting;
        _billing = billing;
        _app = app;
    }

    public async Task<Modelo303QuarterData> GetQuarterAsync(
        Guid tenantId, int year, int quarter, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = FiscalQuarterHelper.QuarterRange(year, quarter);

        var invoices = await _billing.Invoices
            .Include(i => i.InvoiceLines)
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking().ToListAsync(ct);

        var allLines = invoices.SelectMany(i => i.InvoiceLines).ToList();

        var nacional = allLines
            .Where(l => l.TipoOperacion == "Nacional")
            .GroupBy(l => l.TaxRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var (casBase, casCuota) = FiscalQuarterHelper.RateToCasillas(g.Key);
                return new Modelo303RateLine(
                    g.Key,
                    Math.Round(g.Sum(l => l.LineTotal), 2),
                    Math.Round(g.Sum(l => l.TaxAmount), 2),
                    casBase, casCuota);
            }).ToList();

        var recargo = allLines
            .Where(l => l.TipoOperacion == "Nacional" && l.SurchargeRate > 0)
            .GroupBy(l => l.SurchargeRate)
            .OrderByDescending(g => g.Key)
            .Select(g =>
            {
                var (casBase, casCuota) = FiscalQuarterHelper.SurchargeRateToCasillas(g.Key);
                return new Modelo303RecargoLine(
                    g.Key, Math.Round(g.Sum(l => l.LineTotal), 2),
                    Math.Round(g.Sum(l => l.SurchargeAmount), 2), casBase, casCuota);
            }).ToList();

        var intracom = Math.Round(
            allLines.Where(l => l.TipoOperacion == "IntraComunitario").Sum(l => l.LineTotal), 2);
        var exportac = Math.Round(
            allLines.Where(l => l.TipoOperacion == "Exportacion").Sum(l => l.LineTotal), 2);

        var ivaDeducible = Math.Round(
            (await _accounting.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.JournalEntry.CompanyId == tenantId
                         && l.AccountCode.StartsWith("472")
                         && l.JournalEntry.Date >= from && l.JournalEntry.Date < to)
                .AsNoTracking().ToListAsync(ct))
            .Sum(l => l.Debit), 2);

        var totalDevengado = Math.Round(
            nacional.Sum(x => x.Cuota) + recargo.Sum(x => x.Cuota), 2);
        var resultado = totalDevengado - ivaDeducible;

        return new Modelo303QuarterData(
            year, quarter, from, to,
            company?.TaxId, company?.Name,
            nacional, recargo, intracom, exportac,
            ivaDeducible, totalDevengado, resultado);
    }
}

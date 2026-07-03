using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Infrastructure.Services;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo111Reader : IModelo111Reader
{
    private readonly IBillingDbContext _billing;
    private readonly IPayrollDbContext _payroll;
    private readonly IApplicationDbContext _app;

    public Modelo111Reader(
        IBillingDbContext billing,
        IPayrollDbContext payroll,
        IApplicationDbContext app)
    {
        _billing = billing;
        _payroll = payroll;
        _app = app;
    }

    public async Task<Modelo111Result> GetAsync(Guid tenantId, int year, int quarter, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var (from, to) = FiscalQuarterHelper.QuarterRange(year, quarter);

        var invoices = await _billing.Invoices
            .Where(i => i.CompanyId == tenantId && i.IsLocked
                     && i.IrpfAmount > 0
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking()
            .OrderBy(i => i.IssueDate)
            .Select(i => new Modelo111InvoiceLine(
                i.Number,
                i.IssueDate,
                i.ClientNif,
                i.ClientName,
                i.Subtotal,
                i.IrpfRate,
                i.IrpfAmount))
            .ToListAsync(ct);

        var baseTotal = Math.Round(invoices.Sum(i => i.BaseRetencion), 2);
        var importeTotal = Math.Round(invoices.Sum(i => i.ImporteRetencion), 2);

        var monthStart = (quarter - 1) * 3 + 1;
        var monthEnd = quarter * 3;
        var nominas = await _payroll.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement != null
                     && l.Settlement!.Year == year
                     && l.Settlement.Month >= monthStart && l.Settlement.Month <= monthEnd
                     && l.Settlement.Status == "Final")
            .AsNoTracking()
            .OrderBy(l => l.Employee!.TaxId)
            .Select(l => new Modelo111NominaLine(
                l.Employee!.TaxId,
                l.Employee.FullName,
                l.Settlement!.Month,
                l.IrpfBase,
                l.IrpfRate,
                l.IrpfWithheld))
            .ToListAsync(ct);

        var baseNominas = Math.Round(nominas.Sum(n => n.BaseRetencion), 2);
        var importeNominas = Math.Round(nominas.Sum(n => n.ImporteRetencion), 2);

        return new Modelo111Result(
            year,
            quarter,
            $"T{quarter} {year} ({from:dd/MM/yyyy} – {to.AddDays(-1):dd/MM/yyyy})",
            company?.TaxId,
            company?.Name,
            invoices.Select(i => i.ClienteNif).Distinct().Count(),
            baseTotal,
            importeTotal,
            nominas.Select(n => n.Nif).Distinct().Count(),
            baseNominas,
            importeNominas,
            Math.Round(importeTotal + importeNominas, 2),
            invoices,
            nominas);
    }
}

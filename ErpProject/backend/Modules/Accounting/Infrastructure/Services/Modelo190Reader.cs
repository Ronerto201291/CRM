using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Features.Export;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Infrastructure.Services;

public class Modelo190Reader : IModelo190Reader
{
    private readonly IBillingDbContext _billing;
    private readonly IPayrollDbContext _payroll;
    private readonly IApplicationDbContext _app;

    public Modelo190Reader(
        IBillingDbContext billing,
        IPayrollDbContext payroll,
        IApplicationDbContext app)
    {
        _billing = billing;
        _payroll = payroll;
        _app = app;
    }

    public async Task<Modelo190Result> GetAsync(Guid tenantId, int year, CancellationToken ct)
    {
        var company = await _app.Companies.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == tenantId, ct);
        var from = new DateTime(year, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var prof = await _billing.Invoices
            .Where(i => i.CompanyId == tenantId && i.IsLocked && i.IrpfAmount > 0
                     && i.IssueDate >= from && i.IssueDate < to)
            .AsNoTracking()
            .GroupBy(i => new { i.ClientNif, i.ClientName })
            .Select(g => new Modelo190Perceptor(
                g.Key.ClientNif,
                g.Key.ClientName,
                g.Sum(i => i.Subtotal),
                g.Sum(i => i.IrpfAmount)))
            .ToListAsync(ct);

        var trab = await _payroll.PayrollLines
            .Include(l => l.Settlement)
            .Include(l => l.Employee)
            .Where(l => l.Settlement != null && l.Settlement!.Year == year && l.Settlement.Status == "Final")
            .AsNoTracking()
            .GroupBy(l => new { l.Employee!.TaxId, l.Employee.FullName })
            .Select(g => new Modelo190Perceptor(
                g.Key.TaxId,
                g.Key.FullName,
                g.Sum(l => l.IrpfBase),
                g.Sum(l => l.IrpfWithheld)))
            .ToListAsync(ct);

        return new Modelo190Result(
            year,
            company?.TaxId,
            company?.Name,
            "Datos orientativos para el 190; contrastar con modelo oficial y asesoría.",
            prof,
            trab,
            Math.Round(prof.Sum(p => p.Retencion), 2),
            Math.Round(trab.Sum(t => t.Retencion), 2));
    }
}

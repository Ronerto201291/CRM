using Erp.Application.Common.Interfaces;
using Erp.Modules.Payroll.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Payroll.Infrastructure.Services;

public sealed class AutomationPayrollQuery : IAutomationPayrollQuery
{
    private readonly IPayrollDbContext _payroll;

    public AutomationPayrollQuery(IPayrollDbContext payroll) => _payroll = payroll;

    public async Task<decimal> GetLatestMonthlyPayrollCostAsync(Guid companyId, CancellationToken ct = default)
    {
        var latest = await _payroll.PayrollSettlements
            .IgnoreQueryFilters()
            .Where(s => s.CompanyId == companyId && s.Status == "Final")
            .OrderByDescending(s => s.Year)
            .ThenByDescending(s => s.Month)
            .Select(s => s.Id)
            .FirstOrDefaultAsync(ct);

        if (latest == Guid.Empty)
            return 0m;

        return await _payroll.PayrollLines
            .IgnoreQueryFilters()
            .Where(l => l.PayrollSettlementId == latest)
            .SumAsync(l => l.GrossSalary + l.EmployerSocialSecurity, ct);
    }
}

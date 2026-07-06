using Erp.Application.Common.Interfaces;
using Erp.Modules.Crm.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Crm.Infrastructure.Services;

public sealed class AutomationRecurringQuery : IAutomationRecurringQuery
{
    private readonly ICrmDbContext _crm;

    public AutomationRecurringQuery(ICrmDbContext crm) => _crm = crm;

    public async Task<decimal> GetMonthlyRecurringInflowAsync(Guid companyId, CancellationToken ct = default)
    {
        var contracts = await _crm.ClientContractedServices
            .IgnoreQueryFilters()
            .Where(c => c.CompanyId == companyId && c.Status == "Active")
            .AsNoTracking()
            .ToListAsync(ct);

        return contracts.Sum(c => NormalizeToMonthly(c.Price, c.TaxRate, c.Periodicity));
    }

    private static decimal NormalizeToMonthly(decimal price, decimal taxRate, string periodicity)
    {
        var gross = price * (1 + taxRate / 100m);
        return periodicity switch
        {
            "Quarterly" => gross / 3m,
            "Yearly" => gross / 12m,
            _ => gross,
        };
    }
}

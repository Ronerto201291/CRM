using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Billing.Infrastructure.Services;

public sealed class AutomationBillingQuery : IAutomationBillingQuery
{
    private readonly IBillingDbContext _billing;

    public AutomationBillingQuery(IBillingDbContext billing) => _billing = billing;

    public async Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetOverdueInvoicesAsync(
        DateTime today, CancellationToken ct = default)
    {
        return await _billing.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.DueDate.Date < today
                && i.Status != "Paid"
                && i.Status != "Cancelled"
                && i.Status != "Draft")
            .Select(i => new AutomationInvoiceSnapshot(
                i.Id, i.CompanyId, i.Number, i.ClientName,
                i.Total, i.Subtotal, i.TaxAmount, i.Status, i.DueDate, i.CreatedAt))
            .AsNoTracking()
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AutomationInvoiceSnapshot>> GetInvoicesForRuleAsync(
        Guid companyId, string triggerEvent, DateTime today, CancellationToken ct = default)
    {
        var query = _billing.Invoices
            .IgnoreQueryFilters()
            .Where(i => i.CompanyId == companyId && i.Status != "Cancelled" && i.Status != "Draft");

        if (triggerEvent == "OnInvoiceOverdue")
            query = query.Where(i => i.DueDate.Date < today && i.Status != "Paid");
        else if (triggerEvent == "OnInvoiceCreated")
        {
            var since = DateTime.UtcNow.AddDays(-7);
            query = query.Where(i => i.CreatedAt >= since);
        }

        return await query
            .Select(i => new AutomationInvoiceSnapshot(
                i.Id, i.CompanyId, i.Number, i.ClientName,
                i.Total, i.Subtotal, i.TaxAmount, i.Status, i.DueDate, i.CreatedAt))
            .AsNoTracking()
            .ToListAsync(ct);
    }
}

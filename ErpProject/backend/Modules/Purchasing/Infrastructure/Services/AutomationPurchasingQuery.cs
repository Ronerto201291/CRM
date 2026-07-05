using Erp.Application.Common.Interfaces;
using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Purchasing.Infrastructure.Services;

public sealed class AutomationPurchasingQuery : IAutomationPurchasingQuery
{
    private readonly IPurchasingDbContext _purchasing;

    public AutomationPurchasingQuery(IPurchasingDbContext purchasing) => _purchasing = purchasing;

    public async Task<IReadOnlyList<AutomationPendingPurchaseOrder>> GetPendingPurchaseOrderApprovalsAsync(
        CancellationToken ct = default)
    {
        var orders = await _purchasing.PurchaseOrders
            .IgnoreQueryFilters()
            .Include(p => p.Lines)
            .Where(p => p.Status == PurchaseOrderStatuses.PendingApproval)
            .AsNoTracking()
            .ToListAsync(ct);

        return orders.Select(p => new AutomationPendingPurchaseOrder(
            p.Id, p.CompanyId, p.Number, p.TotalAmount, p.CreatedAt)).ToList();
    }
}

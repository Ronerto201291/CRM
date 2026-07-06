using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Infrastructure.Services;

public sealed class AutomationInventoryQuery : IAutomationInventoryQuery
{
    private readonly IInventoryDbContext _inventory;

    public AutomationInventoryQuery(IInventoryDbContext inventory) => _inventory = inventory;

    public async Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsWithReorderPointAsync(
        CancellationToken ct = default)
    {
        var products = await _inventory.InventoryProducts
            .IgnoreQueryFilters()
            .Where(p => p.ReorderPoint > 0)
            .Select(p => new { p.Id, p.CompanyId, p.Name, p.SKU, p.ReorderPoint, p.ReorderQty })
            .AsNoTracking()
            .ToListAsync(ct);

        if (products.Count == 0) return Array.Empty<AutomationProductStockSnapshot>();

        var stockMap = await GetStockMapAsync(ct);
        return products.Select(p => new AutomationProductStockSnapshot(
            p.Id, p.CompanyId, p.Name, p.SKU, p.ReorderPoint, p.ReorderQty,
            stockMap.GetValueOrDefault(p.Id, 0))).ToList();
    }

    public async Task<IReadOnlyList<AutomationProductStockSnapshot>> GetProductsBelowReorderForCompanyAsync(
        Guid companyId, CancellationToken ct = default)
    {
        var all = await GetProductsWithReorderPointAsync(ct);
        return all.Where(p => p.CompanyId == companyId && p.CurrentStock <= p.ReorderPoint).ToList();
    }

    private async Task<Dictionary<Guid, decimal>> GetStockMapAsync(CancellationToken ct)
    {
        var rows = await _inventory.Stocks
            .IgnoreQueryFilters()
            .GroupBy(s => s.ProductId)
            .Select(g => new { ProductId = g.Key, TotalQty = g.Sum(s => s.Quantity) })
            .AsNoTracking()
            .ToListAsync(ct);

        return rows.ToDictionary(x => x.ProductId, x => x.TotalQty);
    }
}

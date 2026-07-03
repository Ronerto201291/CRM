using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Application.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetInventoryValuationHandler : IRequestHandler<GetInventoryValuationQuery, IReadOnlyList<InventoryValuationItemDto>>
{
    private readonly IInventoryDbContext _ctx;

    public GetInventoryValuationHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<IReadOnlyList<InventoryValuationItemDto>> Handle(GetInventoryValuationQuery request, CancellationToken ct)
    {
        var valuationMethod = request.Method ?? "PMP";
        var strategy = InventoryValuationService.CreateStrategy(valuationMethod);

        var products = await _ctx.InventoryProducts.AsNoTracking().ToListAsync(ct);
        var stocks = await _ctx.Stocks.AsNoTracking().ToListAsync(ct);
        var movements = await _ctx.StockMovements.AsNoTracking().ToListAsync(ct);

        return products.Select(p =>
        {
            var productMovements = movements.Where(m => m.ProductId == p.Id).ToList();
            var unitCost = strategy.CalculateUnitCost(productMovements);
            var qty = stocks.Where(s => s.ProductId == p.Id).Sum(s => s.Quantity);
            return new InventoryValuationItemDto(p.Id, p.Name, qty, unitCost, qty * unitCost);
        }).ToList();
    }
}

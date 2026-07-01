using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetStockHandler : IRequestHandler<GetStockQuery, List<StockItemDto>>
{
    private readonly IInventoryDbContext _ctx;
    public GetStockHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<List<StockItemDto>> Handle(GetStockQuery request, CancellationToken ct)
    {
        var query = _ctx.Stocks
            .Include(s => s.Product)
            .Include(s => s.Warehouse)
            .AsQueryable();

        if (request.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == request.WarehouseId.Value);
        if (request.ProductId.HasValue)
            query = query.Where(s => s.ProductId == request.ProductId.Value);

        var stock = await query
            .OrderBy(s => s.Warehouse!.Name).ThenBy(s => s.Product!.Name)
            .Select(s => new StockItemDto
            {
                Id = s.Id, ProductId = s.ProductId,
                ProductSKU = s.Product!.SKU, ProductName = s.Product.Name, ProductType = s.Product.Type,
                WarehouseId = s.WarehouseId, WarehouseName = s.Warehouse!.Name,
                Quantity = s.Quantity, CostPrice = s.Product.CostPrice, SalePrice = s.Product.SalePrice,
                ReorderPoint = s.Product.ReorderPoint, ReorderQty = s.Product.ReorderQty,
                BelowReorderPoint = s.Quantity <= s.Product.ReorderPoint,
                StockValue = s.Quantity * s.Product.CostPrice, UpdatedAt = s.UpdatedAt
            })
            .ToListAsync(ct);

        if (request.BelowReorderPoint == true)
            stock = stock.Where(s => s.BelowReorderPoint).ToList();

        return stock;
    }
}

public class GetStockByProductHandler : IRequestHandler<GetStockByProductQuery, StockByProductDto?>
{
    private readonly IInventoryDbContext _ctx;
    public GetStockByProductHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<StockByProductDto?> Handle(GetStockByProductQuery request, CancellationToken ct)
    {
        var product = await _ctx.InventoryProducts.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct);
        if (product == null) return null;

        var warehouses = await _ctx.Stocks
            .Where(s => s.ProductId == request.ProductId)
            .Select(s => new StockWarehouseItemDto
            {
                WarehouseId = s.WarehouseId, WarehouseName = s.Warehouse!.Name,
                Quantity = s.Quantity, StockValue = s.Quantity * product.CostPrice
            })
            .ToListAsync(ct);

        return new StockByProductDto
        {
            Id = product.Id, SKU = product.SKU, Name = product.Name,
            CostPrice = product.CostPrice, SalePrice = product.SalePrice,
            ReorderPoint = product.ReorderPoint, ReorderQty = product.ReorderQty,
            TotalStock = warehouses.Sum(w => w.Quantity),
            TotalValue = warehouses.Sum(w => w.StockValue),
            Warehouses = warehouses
        };
    }
}

public class GetStockValuationHandler : IRequestHandler<GetStockValuationQuery, StockValuationResult>
{
    private readonly IInventoryDbContext _ctx;
    public GetStockValuationHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<StockValuationResult> Handle(GetStockValuationQuery request, CancellationToken ct)
    {
        var query = _ctx.Stocks.Include(s => s.Product).AsQueryable();
        if (request.WarehouseId.HasValue)
            query = query.Where(s => s.WarehouseId == request.WarehouseId.Value);

        var lines = await query
            .GroupBy(s => new { s.ProductId, s.Product!.SKU, s.Product.Name, s.Product.CostPrice, s.Product.SalePrice })
            .Select(g => new StockValuationLineDto
            {
                ProductId = g.Key.ProductId, SKU = g.Key.SKU, Name = g.Key.Name,
                CostPrice = g.Key.CostPrice, SalePrice = g.Key.SalePrice,
                TotalQty = g.Sum(s => s.Quantity),
                TotalCostValue = g.Sum(s => s.Quantity) * g.Key.CostPrice,
                TotalSaleValue = g.Sum(s => s.Quantity) * g.Key.SalePrice
            })
            .OrderByDescending(r => r.TotalCostValue)
            .ToListAsync(ct);

        return new StockValuationResult
        {
            GeneratedAt = DateTime.UtcNow,
            TotalStockCost = lines.Sum(r => r.TotalCostValue),
            TotalStockAtSale = lines.Sum(r => r.TotalSaleValue),
            TotalLines = lines.Count,
            Lines = lines
        };
    }
}

public class GetStockMovementsHandler : IRequestHandler<GetStockMovementsQuery, StockMovementsResult>
{
    private readonly IInventoryDbContext _ctx;
    public GetStockMovementsHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<StockMovementsResult> Handle(GetStockMovementsQuery request, CancellationToken ct)
    {
        var page = request.Page < 1 ? 1 : request.Page;
        var pageSize = request.PageSize is < 1 or > 200 ? 50 : request.PageSize;

        var query = _ctx.StockMovements.AsQueryable();
        if (request.ProductId.HasValue)   query = query.Where(m => m.ProductId == request.ProductId.Value);
        if (request.WarehouseId.HasValue) query = query.Where(m => m.WarehouseId == request.WarehouseId.Value);
        if (!string.IsNullOrEmpty(request.MovementType)) query = query.Where(m => m.MovementType == request.MovementType);
        if (request.From.HasValue) query = query.Where(m => m.CreatedAt >= request.From.Value);
        if (request.To.HasValue)   query = query.Where(m => m.CreatedAt < request.To.Value.AddDays(1));

        var total = await query.CountAsync(ct);

        var items = await query
            .OrderByDescending(m => m.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Join(_ctx.InventoryProducts, m => m.ProductId, p => p.Id,
                (m, p) => new StockMovementListDto
                {
                    Id = m.Id, ProductId = m.ProductId, ProductSKU = p.SKU, ProductName = p.Name,
                    WarehouseId = m.WarehouseId, MovementType = m.MovementType,
                    Quantity = m.Quantity, UnitCost = m.UnitCost, TotalCost = m.Quantity * m.UnitCost,
                    ReferenceType = m.ReferenceType, ReferenceId = m.ReferenceId, CreatedAt = m.CreatedAt
                })
            .ToListAsync(ct);

        return new StockMovementsResult { Total = total, Page = page, PageSize = pageSize, Items = items };
    }
}

public class AdjustStockHandler : IRequestHandler<AdjustStockCommand, AdjustStockResult>
{
    private readonly IInventoryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public AdjustStockHandler(IInventoryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<AdjustStockResult> Handle(AdjustStockCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var product = await _ctx.InventoryProducts.FirstOrDefaultAsync(p => p.Id == request.ProductId, ct)
            ?? throw new KeyNotFoundException("Producto no encontrado.");

        if (await _ctx.Warehouses.FirstOrDefaultAsync(w => w.Id == request.WarehouseId, ct) == null)
            throw new KeyNotFoundException("Almacen no encontrado.");

        if (request.Quantity == 0)
            throw new ArgumentException("La cantidad no puede ser 0.");

        var stock = await _ctx.Stocks
            .FirstOrDefaultAsync(s => s.ProductId == request.ProductId && s.WarehouseId == request.WarehouseId, ct);

        var currentQty = stock?.Quantity ?? 0m;

        if (currentQty + request.Quantity < 0)
            throw new InvalidOperationException(
                $"Stock insuficiente. Disponible: {currentQty}, ajuste: {request.Quantity}.");

        if (request.Quantity > 0 && request.UnitCost.HasValue && request.UnitCost.Value > 0)
        {
            var newQty = currentQty + request.Quantity;
            product.CostPrice = Math.Round(
                ((currentQty * product.CostPrice) + (request.Quantity * request.UnitCost.Value)) / newQty, 4);
            product.UpdatedAt = DateTime.UtcNow;
        }

        var movement = new StockMovement
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            ProductId = request.ProductId, WarehouseId = request.WarehouseId,
            MovementType = request.Quantity > 0 ? "Entry" : "Adjustment",
            Quantity = request.Quantity, UnitCost = request.UnitCost ?? product.CostPrice,
            ReferenceType = "Manual", ReferenceId = null, CreatedAt = DateTime.UtcNow
        };
        _ctx.StockMovements.Add(movement);

        if (stock != null)
        {
            stock.Quantity  += request.Quantity;
            stock.UpdatedAt  = DateTime.UtcNow;
        }
        else
        {
            _ctx.Stocks.Add(new Stock
            {
                Id = Guid.NewGuid(), CompanyId = companyId,
                ProductId = request.ProductId, WarehouseId = request.WarehouseId,
                Quantity = request.Quantity, CreatedAt = DateTime.UtcNow
            });
        }

        await _ctx.SaveChangesAsync(ct);

        var finalQty = stock != null ? stock.Quantity : request.Quantity;
        return new AdjustStockResult
        {
            Message = "Ajuste registrado.", MovementId = movement.Id,
            NewQuantity = finalQty, NewCostPrice = product.CostPrice,
            BelowReorderPoint = finalQty <= product.ReorderPoint
        };
    }
}

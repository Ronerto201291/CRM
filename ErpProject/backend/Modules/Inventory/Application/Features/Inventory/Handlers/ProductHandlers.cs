using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetProductsHandler : IRequestHandler<GetProductsQuery, PaginatedProductsResult>
{
    private readonly IInventoryDbContext _ctx;
    public GetProductsHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<PaginatedProductsResult> Handle(GetProductsQuery request, CancellationToken ct)
    {
        var page = Math.Max(1, request.Page);
        var pageSize = Math.Clamp(request.PageSize, 1, 500);

        var query = _ctx.InventoryProducts.AsQueryable();

        if (!string.IsNullOrWhiteSpace(request.Search))
            query = query.Where(p => p.Name.Contains(request.Search) || p.SKU.Contains(request.Search));
        if (request.Active.HasValue)
            query = query.Where(p => p.IsActive == request.Active.Value);
        if (!string.IsNullOrWhiteSpace(request.Type))
            query = query.Where(p => p.Type == request.Type);

        var totalCount = await query.CountAsync(ct);

        var items = await query
            .OrderBy(p => p.Name)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(p => new ProductListDto
            {
                Id = p.Id, SKU = p.SKU, Name = p.Name, Description = p.Description,
                Type = p.Type, CostPrice = p.CostPrice, SalePrice = p.SalePrice,
                VatPercent = p.VatPercent, TrackStock = p.TrackStock, IsActive = p.IsActive,
                CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt,
                TotalStock = _ctx.Stocks.Where(s => s.ProductId == p.Id).Sum(s => (decimal?)s.Quantity) ?? 0m
            })
            .ToListAsync(ct);

        return new PaginatedProductsResult(items, totalCount, page, pageSize);
    }
}

public class GetProductByIdHandler : IRequestHandler<GetProductByIdQuery, ProductDetailDto?>
{
    private readonly IInventoryDbContext _ctx;
    public GetProductByIdHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<ProductDetailDto?> Handle(GetProductByIdQuery request, CancellationToken ct)
    {
        return await _ctx.InventoryProducts
            .Where(p => p.Id == request.Id)
            .Select(p => new ProductDetailDto
            {
                Id = p.Id, SKU = p.SKU, Name = p.Name, Description = p.Description,
                Type = p.Type, CostPrice = p.CostPrice, SalePrice = p.SalePrice,
                VatPercent = p.VatPercent, TrackStock = p.TrackStock, IsActive = p.IsActive,
                CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt,
                StockByWarehouse = _ctx.Stocks
                    .Where(s => s.ProductId == p.Id)
                    .Select(s => new ProductStockByWarehouseDto
                    {
                        WarehouseId = s.WarehouseId, Name = s.Warehouse!.Name, Quantity = s.Quantity
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class CreateProductHandler : IRequestHandler<CreateProductCommand, CreateProductResult>
{
    private readonly IInventoryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateProductHandler(IInventoryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<CreateProductResult> Handle(CreateProductCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        if (await _ctx.InventoryProducts.AnyAsync(p => p.SKU == request.SKU, ct))
            throw new InvalidOperationException($"SKU '{request.SKU}' ya existe para este tenant.");

        var product = new Product
        {
            Id          = Guid.NewGuid(),
            CompanyId   = companyId,
            SKU         = request.SKU,
            Name        = request.Name,
            Description = request.Description ?? string.Empty,
            Type        = string.IsNullOrWhiteSpace(request.Type) ? "Product" : request.Type,
            CostPrice   = request.CostPrice ?? 0m,
            SalePrice   = request.SalePrice ?? 0m,
            VatPercent  = request.VatPercent ?? 21m,
            TrackStock  = request.TrackStock ?? true,
            IsActive    = true,
            CreatedAt   = DateTime.UtcNow
        };

        _ctx.InventoryProducts.Add(product);
        await _ctx.SaveChangesAsync(ct);

        return new CreateProductResult { Id = product.Id, SKU = product.SKU, Name = product.Name };
    }
}

public class UpdateProductHandler : IRequestHandler<UpdateProductCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public UpdateProductHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateProductCommand request, CancellationToken ct)
    {
        var product = await _ctx.InventoryProducts.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (product == null) return false;

        product.Name        = request.Name        ?? product.Name;
        product.Description = request.Description ?? product.Description;
        product.Type        = request.Type        ?? product.Type;
        product.SalePrice   = request.SalePrice   ?? product.SalePrice;
        product.VatPercent  = request.VatPercent  ?? product.VatPercent;
        product.TrackStock  = request.TrackStock  ?? product.TrackStock;
        product.UpdatedAt   = DateTime.UtcNow;

        if (request.SKU != null && request.SKU != product.SKU)
        {
            if (await _ctx.InventoryProducts.AnyAsync(p => p.SKU == request.SKU && p.Id != request.Id, ct))
                throw new InvalidOperationException($"SKU '{request.SKU}' ya existe.");
            product.SKU = request.SKU;
        }

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class SetProductActiveHandler : IRequestHandler<SetProductActiveCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public SetProductActiveHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(SetProductActiveCommand request, CancellationToken ct)
    {
        var product = await _ctx.InventoryProducts.FirstOrDefaultAsync(p => p.Id == request.Id, ct);
        if (product == null) return false;

        product.IsActive  = request.IsActive;
        product.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class GetProductMovementsHandler : IRequestHandler<GetProductMovementsQuery, List<ProductMovementDto>>
{
    private readonly IInventoryDbContext _ctx;
    public GetProductMovementsHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<List<ProductMovementDto>> Handle(GetProductMovementsQuery request, CancellationToken ct)
    {
        return await _ctx.StockMovements
            .Where(m => m.ProductId == request.ProductId)
            .OrderByDescending(m => m.CreatedAt)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(m => new ProductMovementDto
            {
                Id = m.Id, MovementType = m.MovementType, Quantity = m.Quantity,
                UnitCost = m.UnitCost, TotalCost = m.Quantity * m.UnitCost,
                ReferenceType = m.ReferenceType, ReferenceId = m.ReferenceId,
                WarehouseId = m.WarehouseId, CreatedAt = m.CreatedAt
            })
            .ToListAsync(ct);
    }
}

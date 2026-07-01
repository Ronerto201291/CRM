using Erp.Application.Common.Interfaces;
using Erp.Modules.Inventory.Application.Features.Inventory.Commands;
using Erp.Modules.Inventory.Application.Features.Inventory.Queries;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Inventory.Application.Features.Inventory.Handlers;

public class GetWarehousesHandler : IRequestHandler<GetWarehousesQuery, List<WarehouseListDto>>
{
    private readonly IInventoryDbContext _ctx;
    public GetWarehousesHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<List<WarehouseListDto>> Handle(GetWarehousesQuery request, CancellationToken ct)
    {
        var query = _ctx.Warehouses.AsQueryable();
        if (request.Active.HasValue)
            query = query.Where(w => w.IsActive == request.Active.Value);

        return await query
            .OrderBy(w => w.Name)
            .Select(w => new WarehouseListDto
            {
                Id = w.Id, Name = w.Name, Location = w.Location,
                IsActive = w.IsActive, CreatedAt = w.CreatedAt,
                ProductCount = _ctx.Stocks.Count(s => s.WarehouseId == w.Id && s.Quantity > 0)
            })
            .ToListAsync(ct);
    }
}

public class GetWarehouseByIdHandler : IRequestHandler<GetWarehouseByIdQuery, WarehouseDetailDto?>
{
    private readonly IInventoryDbContext _ctx;
    public GetWarehouseByIdHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<WarehouseDetailDto?> Handle(GetWarehouseByIdQuery request, CancellationToken ct)
    {
        return await _ctx.Warehouses
            .Where(w => w.Id == request.Id)
            .Select(w => new WarehouseDetailDto
            {
                Id = w.Id, Name = w.Name, Location = w.Location,
                IsActive = w.IsActive, CreatedAt = w.CreatedAt, UpdatedAt = w.UpdatedAt,
                Stock = _ctx.Stocks
                    .Where(s => s.WarehouseId == w.Id)
                    .Select(s => new WarehouseStockItemDto
                    {
                        ProductId = s.ProductId, ProductName = s.Product!.Name,
                        ProductSKU = s.Product.SKU, Quantity = s.Quantity,
                        CostPrice = s.Product.CostPrice, StockValue = s.Quantity * s.Product.CostPrice
                    })
                    .OrderBy(s => s.ProductName)
                    .ToList()
            })
            .FirstOrDefaultAsync(ct);
    }
}

public class CreateWarehouseHandler : IRequestHandler<CreateWarehouseCommand, CreateWarehouseResult>
{
    private readonly IInventoryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateWarehouseHandler(IInventoryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx; _tenant = tenant;
    }

    public async Task<CreateWarehouseResult> Handle(CreateWarehouseCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new UnauthorizedAccessException("No tenant context.");

        var warehouse = new Warehouse
        {
            Id = Guid.NewGuid(), CompanyId = companyId,
            Name = request.Name, Location = request.Location ?? string.Empty,
            IsActive = true, CreatedAt = DateTime.UtcNow
        };

        _ctx.Warehouses.Add(warehouse);
        await _ctx.SaveChangesAsync(ct);

        return new CreateWarehouseResult { Id = warehouse.Id, Name = warehouse.Name, Location = warehouse.Location };
    }
}

public class UpdateWarehouseHandler : IRequestHandler<UpdateWarehouseCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public UpdateWarehouseHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(UpdateWarehouseCommand request, CancellationToken ct)
    {
        var warehouse = await _ctx.Warehouses.FirstOrDefaultAsync(w => w.Id == request.Id, ct);
        if (warehouse == null) return false;

        warehouse.Name = request.Name ?? warehouse.Name;
        warehouse.Location = request.Location ?? warehouse.Location;
        warehouse.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class SetWarehouseActiveHandler : IRequestHandler<SetWarehouseActiveCommand, bool>
{
    private readonly IInventoryDbContext _ctx;
    public SetWarehouseActiveHandler(IInventoryDbContext ctx) => _ctx = ctx;

    public async Task<bool> Handle(SetWarehouseActiveCommand request, CancellationToken ct)
    {
        var warehouse = await _ctx.Warehouses.FirstOrDefaultAsync(w => w.Id == request.Id, ct);
        if (warehouse == null) return false;

        warehouse.IsActive = request.IsActive;
        warehouse.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

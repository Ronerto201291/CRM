using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.Events;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Increments stock when a goods receipt is recorded in Purchasing.
/// </summary>
public class GoodsReceiptInventoryHandler : INotificationHandler<GoodsReceiptCreatedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<GoodsReceiptInventoryHandler> _logger;

    public GoodsReceiptInventoryHandler(
        IInventoryDbContext context,
        IPublisher publisher,
        ILogger<GoodsReceiptInventoryHandler> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(GoodsReceiptCreatedEvent notification, CancellationToken cancellationToken)
    {
        var exists = await _context.StockMovements
            .AnyAsync(m => m.ReferenceType == "GoodsReceipt"
                        && m.ReferenceId == notification.GoodsReceiptId
                        && m.CompanyId == notification.CompanyId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Stock movements for goods receipt {ReceiptId} already exist, skipping.", notification.GoodsReceiptId);
            return;
        }

        var productLines = notification.Lines
            .Where(l => l.ProductId.HasValue && l.Quantity > 0)
            .ToList();
        if (!productLines.Any()) return;

        var warehouse = await _context.Warehouses
            .FirstOrDefaultAsync(w => w.CompanyId == notification.CompanyId && w.IsActive, cancellationToken);
        if (warehouse == null)
        {
            _logger.LogWarning("No active warehouse for company {CompanyId}. Skipping goods receipt stock.", notification.CompanyId);
            return;
        }

        var createdEvents = new List<StockMovementCreatedEvent>();

        foreach (var line in productLines)
        {
            var product = await _context.InventoryProducts
                .FirstOrDefaultAsync(p => p.Id == line.ProductId!.Value && p.CompanyId == notification.CompanyId, cancellationToken);
            if (product == null || !product.TrackStock) continue;

            var stock = await _context.Stocks
                .FirstOrDefaultAsync(s =>
                    s.ProductId == line.ProductId!.Value &&
                    s.WarehouseId == warehouse.Id &&
                    s.CompanyId == notification.CompanyId, cancellationToken);

            var currentQty = stock?.Quantity ?? 0m;
            var unitCost = line.UnitCost > 0 ? line.UnitCost : product.CostPrice;
            if (line.Quantity > 0 && unitCost > 0)
            {
                var newQty = currentQty + line.Quantity;
                product.CostPrice = Math.Round(
                    ((currentQty * product.CostPrice) + (line.Quantity * unitCost)) / newQty, 4);
                product.UpdatedAt = DateTime.UtcNow;
            }

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = notification.CompanyId,
                ProductId = line.ProductId!.Value,
                WarehouseId = warehouse.Id,
                MovementType = "Purchase",
                Quantity = line.Quantity,
                UnitCost = unitCost,
                ReferenceType = "GoodsReceipt",
                ReferenceId = notification.GoodsReceiptId,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockMovements.Add(movement);

            if (stock != null)
            {
                stock.Quantity += line.Quantity;
                stock.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                _context.Stocks.Add(new Stock
                {
                    Id = Guid.NewGuid(),
                    CompanyId = notification.CompanyId,
                    ProductId = line.ProductId!.Value,
                    WarehouseId = warehouse.Id,
                    Quantity = line.Quantity,
                    CreatedAt = DateTime.UtcNow
                });
            }

            createdEvents.Add(new StockMovementCreatedEvent(
                notification.CompanyId, movement.Id, "Purchase",
                line.Quantity * unitCost, notification.GoodsReceiptId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var evt in createdEvents)
            await _publisher.Publish(evt, cancellationToken);
    }
}

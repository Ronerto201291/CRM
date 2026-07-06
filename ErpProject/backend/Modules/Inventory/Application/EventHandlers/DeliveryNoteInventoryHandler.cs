using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.Events;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Decrements stock when a delivery note is created in Sales.
/// </summary>
public class DeliveryNoteInventoryHandler : INotificationHandler<DeliveryNoteCreatedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<DeliveryNoteInventoryHandler> _logger;

    public DeliveryNoteInventoryHandler(
        IInventoryDbContext context,
        IPublisher publisher,
        ILogger<DeliveryNoteInventoryHandler> logger)
    {
        _context = context;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(DeliveryNoteCreatedEvent notification, CancellationToken cancellationToken)
    {
        var exists = await _context.StockMovements
            .AnyAsync(m => m.ReferenceType == "DeliveryNote"
                        && m.ReferenceId == notification.DeliveryNoteId
                        && m.CompanyId == notification.CompanyId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Stock movements for delivery note {DeliveryId} already exist, skipping.", notification.DeliveryNoteId);
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
            _logger.LogWarning("No active warehouse for company {CompanyId}. Skipping delivery stock.", notification.CompanyId);
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
            if (currentQty < line.Quantity)
            {
                _logger.LogWarning(
                    "Stock insuficiente para producto {ProductId}: disponible={Current}, requerido={Required}. Movimiento omitido.",
                    line.ProductId, currentQty, line.Quantity);
                continue;
            }

            var movement = new StockMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = notification.CompanyId,
                ProductId = line.ProductId!.Value,
                WarehouseId = warehouse.Id,
                MovementType = "Sale",
                Quantity = -line.Quantity,
                UnitCost = product.CostPrice,
                ReferenceType = "DeliveryNote",
                ReferenceId = notification.DeliveryNoteId,
                CreatedAt = DateTime.UtcNow
            };
            _context.StockMovements.Add(movement);

            if (stock != null)
                stock.Quantity -= line.Quantity;

            createdEvents.Add(new StockMovementCreatedEvent(
                notification.CompanyId, movement.Id, "Sale",
                line.Quantity * product.CostPrice, notification.DeliveryNoteId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var evt in createdEvents)
            await _publisher.Publish(evt, cancellationToken);
    }
}

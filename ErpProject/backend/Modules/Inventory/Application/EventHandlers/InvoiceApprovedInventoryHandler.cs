using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.Events;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Handles InvoiceApprovedEvent to decrement stock for sold products.
/// After committing stock changes, publishes StockMovementCreatedEvent per line
/// so the Outbox handler can relay it to external subscribers.
/// </summary>
public class InvoiceApprovedInventoryHandler : INotificationHandler<InvoiceApprovedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<InvoiceApprovedInventoryHandler> _logger;

    public InvoiceApprovedInventoryHandler(
        IInventoryDbContext context,
        IPublisher publisher,
        ILogger<InvoiceApprovedInventoryHandler> logger)
    {
        _context   = context;
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(InvoiceApprovedEvent notification, CancellationToken cancellationToken)
    {
        // Idempotency: skip if stock movements already created for this invoice
        var exists = await _context.StockMovements
            .AnyAsync(m => m.ReferenceType == "Invoice" && m.ReferenceId == notification.InvoiceId
                        && m.CompanyId == notification.CompanyId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Stock movements for invoice {InvoiceId} already exist, skipping.", notification.InvoiceId);
            return;
        }

        if (notification.SalesOrderId.HasValue)
        {
            _logger.LogInformation(
                "Skipping stock decrement for invoice {InvoiceId}: sales order {SalesOrderId} already decremented stock on delivery.",
                notification.InvoiceId, notification.SalesOrderId);
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
            _logger.LogWarning("No active warehouse found for company {CompanyId}. Skipping stock movement.", notification.CompanyId);
            return;
        }

        // Collect events to publish after SaveChanges (ensures movements are committed first)
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

            var currentQty = stock?.Quantity ?? 0;

            if (currentQty < line.Quantity)
            {
                _logger.LogWarning(
                    "Stock insuficiente para producto {ProductId}: disponible={Current}, requerido={Required}. Movimiento omitido.",
                    line.ProductId, currentQty, line.Quantity);
                continue;
            }

            var movement = new StockMovement
            {
                Id            = Guid.NewGuid(),
                CompanyId     = notification.CompanyId,
                ProductId     = line.ProductId!.Value,
                WarehouseId   = warehouse.Id,
                MovementType  = "Sale",
                Quantity      = -line.Quantity,
                UnitCost      = product.CostPrice,
                ReferenceType = "Invoice",
                ReferenceId   = notification.InvoiceId,
                CreatedAt     = DateTime.UtcNow
            };
            _context.StockMovements.Add(movement);

            if (stock != null)
                stock.Quantity -= line.Quantity;

            _logger.LogInformation(
                "Stock decremented: Product {ProductId}, Qty -{Qty}, Remaining {Remaining}",
                line.ProductId, line.Quantity, stock?.Quantity ?? 0);

            createdEvents.Add(new StockMovementCreatedEvent(
                companyId:       notification.CompanyId,
                stockMovementId: movement.Id,
                movementType:    "Sale",
                totalCost:       line.Quantity * product.CostPrice,
                referenceId:     notification.InvoiceId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        // Publish after commit so StockMovements exist in DB before Outbox is written
        foreach (var evt in createdEvents)
            await _publisher.Publish(evt, cancellationToken);
    }
}

using Erp.Application.Common.Events;
using Erp.Modules.Inventory.Application.Events;
using Erp.Modules.Inventory.Application.Interfaces;
using Erp.Modules.Inventory.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Handles ExpenseApprovedEvent to increment stock for purchased products.
/// Updates weighted average cost (CMP formula) on purchase receipt.
/// After committing, publishes StockMovementCreatedEvent to Outbox.
/// </summary>
public class ExpenseApprovedInventoryHandler : INotificationHandler<ExpenseApprovedEvent>
{
    private readonly IInventoryDbContext _context;
    private readonly IPublisher _publisher;
    private readonly ILogger<ExpenseApprovedInventoryHandler> _logger;

    public ExpenseApprovedInventoryHandler(
        IInventoryDbContext context,
        IPublisher publisher,
        ILogger<ExpenseApprovedInventoryHandler> logger)
    {
        _context   = context;
        _publisher = publisher;
        _logger    = logger;
    }

    public async Task Handle(ExpenseApprovedEvent notification, CancellationToken cancellationToken)
    {
        // Idempotency: skip if stock movements already created for this expense
        var exists = await _context.StockMovements
            .AnyAsync(m => m.ReferenceType == "Expense" && m.ReferenceId == notification.ExpenseDocumentId
                        && m.CompanyId == notification.CompanyId, cancellationToken);
        if (exists)
        {
            _logger.LogWarning("Stock movements for expense {ExpenseId} already exist, skipping.", notification.ExpenseDocumentId);
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
            _logger.LogWarning("No active warehouse for company {CompanyId}.", notification.CompanyId);
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

            // Coste Medio Ponderado
            var currentQty = stock?.Quantity ?? 0;
            var newQty = currentQty + line.Quantity;
            var newWeightedCost = newQty > 0
                ? ((currentQty * product.CostPrice) + (line.Quantity * line.UnitCost)) / newQty
                : line.UnitCost;

            product.CostPrice = Math.Round(newWeightedCost, 4);

            var movement = new StockMovement
            {
                Id            = Guid.NewGuid(),
                CompanyId     = notification.CompanyId,
                ProductId     = line.ProductId!.Value,
                WarehouseId   = warehouse.Id,
                MovementType  = "Purchase",
                Quantity      = line.Quantity,
                UnitCost      = line.UnitCost,
                ReferenceType = "Expense",
                ReferenceId   = notification.ExpenseDocumentId,
                CreatedAt     = DateTime.UtcNow
            };
            _context.StockMovements.Add(movement);

            if (stock != null)
                stock.Quantity += line.Quantity;
            else
                _context.Stocks.Add(new Stock
                {
                    Id          = Guid.NewGuid(),
                    CompanyId   = notification.CompanyId,
                    ProductId   = line.ProductId!.Value,
                    WarehouseId = warehouse.Id,
                    Quantity    = line.Quantity,
                    CreatedAt   = DateTime.UtcNow
                });

            _logger.LogInformation(
                "Stock incremented: Product {ProductId}, Qty +{Qty}, New cost {Cost:F4}",
                line.ProductId, line.Quantity, newWeightedCost);

            createdEvents.Add(new StockMovementCreatedEvent(
                companyId:       notification.CompanyId,
                stockMovementId: movement.Id,
                movementType:    "Purchase",
                totalCost:       line.Quantity * line.UnitCost,
                referenceId:     notification.ExpenseDocumentId));
        }

        await _context.SaveChangesAsync(cancellationToken);

        foreach (var evt in createdEvents)
            await _publisher.Publish(evt, cancellationToken);
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Outbox;
using Erp.Modules.Inventory.Application.Events;
using MediatR;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Erp.Modules.Inventory.Application.EventHandlers;

/// <summary>
/// Handles StockMovementCreatedEvent by writing an Outbox message.
/// External modules (Accounting, Notifications) react via the Outbox relay.
/// </summary>
public class StockMovementCreatedEventHandler : INotificationHandler<StockMovementCreatedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<StockMovementCreatedEventHandler> _logger;

    public StockMovementCreatedEventHandler(
        IApplicationDbContext appCtx,
        ILogger<StockMovementCreatedEventHandler> logger)
    {
        _appCtx = appCtx;
        _logger = logger;
    }

    public async Task Handle(StockMovementCreatedEvent notification, CancellationToken ct)
    {
        var payload = JsonSerializer.Serialize(new
        {
            notification.CompanyId,
            notification.StockMovementId,
            notification.MovementType,
            notification.TotalCost,
            notification.ReferenceId,
            OccurredOn = notification.OccurredOn
        });

        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(StockMovementCreatedEvent),
            Payload   = payload,
            CreatedAt = notification.OccurredOn
        });

        await _appCtx.SaveChangesAsync(ct);

        _logger.LogInformation(
            "StockMovementCreated queued to Outbox: type={Type}, movementId={Id}, company={Company}",
            notification.MovementType, notification.StockMovementId, notification.CompanyId);
    }
}

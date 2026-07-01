using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Outbox;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Erp.Modules.Billing.Application.EventHandlers;

/// <summary>
/// Relays PaymentReceivedEvent to the Outbox table for at-least-once delivery
/// to external subscribers (webhooks, notifications, reporting systems).
///
/// Idempotency: checks OutboxMessages by EventType + PaymentId before inserting.
/// </summary>
public class PaymentReceivedOutboxHandler : INotificationHandler<PaymentReceivedEvent>
{
    private readonly IApplicationDbContext _appCtx;
    private readonly ILogger<PaymentReceivedOutboxHandler> _logger;

    public PaymentReceivedOutboxHandler(IApplicationDbContext appCtx, ILogger<PaymentReceivedOutboxHandler> logger)
    {
        _appCtx  = appCtx;
        _logger  = logger;
    }

    public async Task Handle(PaymentReceivedEvent notification, CancellationToken ct)
    {
        // Idempotency: skip if this payment event is already in the outbox
        var alreadyQueued = await _appCtx.OutboxMessages
            .AnyAsync(m => m.EventType == nameof(PaymentReceivedEvent)
                        && m.CompanyId == notification.CompanyId
                        && m.Payload.Contains(notification.PaymentId.ToString()), ct);
        if (alreadyQueued)
        {
            _logger.LogWarning("PaymentReceivedEvent for PaymentId {PaymentId} already in outbox, skipping.", notification.PaymentId);
            return;
        }

        _appCtx.OutboxMessages.Add(new OutboxMessage
        {
            Id        = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            EventType = nameof(PaymentReceivedEvent),
            Payload   = JsonSerializer.Serialize(notification),
            CreatedAt = DateTime.UtcNow,
            Status    = OutboxMessageStatus.Pending
        });

        await _appCtx.SaveChangesAsync(ct);

        _logger.LogInformation(
            "PaymentReceivedEvent queued to Outbox for invoice {InvoiceNumber} (PaymentId: {PaymentId})",
            notification.InvoiceNumber, notification.PaymentId);
    }
}

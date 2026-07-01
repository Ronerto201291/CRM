using System.Data;
using System.Text.Json;
using Erp.Application.Common.Events;
using Erp.Domain.Entities.Outbox;
using Erp.Infrastructure.Data;
using Hangfire;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Services;

/// <summary>
/// Hangfire job that processes OutboxMessages guaranteeing at-least-once delivery.
/// Reads pending domain events, publishes via MediatR, marks as processed.
/// Run every minute: BackgroundJob.RecurringJob("outbox-processor", ...)
///
/// Distributed-safe: uses SELECT ... FOR UPDATE SKIP LOCKED inside a transaction
/// so each horizontal instance claims its own non-overlapping batch. Rows locked
/// by another instance are skipped rather than contended, preventing duplicate
/// accounting entries or stock movements under horizontal scaling.
/// </summary>
public class OutboxProcessorJob
{
    private readonly ErpDbContext _context;
    private readonly IMediator _mediator;
    private readonly ILogger<OutboxProcessorJob> _logger;

    private static readonly Dictionary<string, Type> EventTypeMap = new()
    {
        [nameof(InvoiceApprovedEvent)]      = typeof(InvoiceApprovedEvent),
        [nameof(ExpenseApprovedEvent)]      = typeof(ExpenseApprovedEvent),
        [nameof(PaymentReceivedEvent)]       = typeof(PaymentReceivedEvent),
        [nameof(QuoteConvertedToInvoiceEvent)] = typeof(QuoteConvertedToInvoiceEvent),
    };

    public OutboxProcessorJob(ErpDbContext context, IMediator mediator, ILogger<OutboxProcessorJob> logger)
    {
        _context = context;
        _mediator = mediator;
        _logger = logger;
    }

    [AutomaticRetry(Attempts = 3)]
    public async Task ProcessPendingAsync(CancellationToken ct = default)
    {
        // Open a transaction so the FOR UPDATE lock is held until we commit.
        // If the process crashes mid-batch the transaction rolls back and the
        // rows become available again — no silent message loss.
        using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);

        // SKIP LOCKED: rows already locked by another instance are skipped
        // entirely, so every running instance works on a disjoint batch.
        var messages = await _context.OutboxMessages
            .FromSqlRaw(@"
                SELECT * FROM ""OutboxMessages""
                WHERE  ""Status""     = 0
                AND    ""RetryCount"" < 5
                ORDER  BY ""CreatedAt""
                LIMIT  50
                FOR UPDATE SKIP LOCKED")
            .ToListAsync(ct);

        foreach (var message in messages)
        {
            try
            {
                if (!EventTypeMap.TryGetValue(message.EventType, out var eventType))
                {
                    _logger.LogWarning("Unknown OutboxMessage event type: {EventType}", message.EventType);
                    message.Status = OutboxMessageStatus.Failed;
                    message.Error = $"Unknown event type: {message.EventType}";
                    continue;
                }

                var domainEvent = (INotification?)JsonSerializer.Deserialize(message.Payload, eventType);
                if (domainEvent == null)
                {
                    message.Status = OutboxMessageStatus.Failed;
                    message.Error = "Failed to deserialize event payload";
                    continue;
                }

                await _mediator.Publish(domainEvent, ct);

                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTime.UtcNow;
                message.Error = null;

                _logger.LogInformation("OutboxMessage processed: {EventType} ({Id})", message.EventType, message.Id);
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                if (message.RetryCount >= 5)
                    message.Status = OutboxMessageStatus.Failed;

                _logger.LogError(ex, "Failed to process OutboxMessage {Id} (attempt {Retry})",
                    message.Id, message.RetryCount);
            }
        }

        await _context.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}

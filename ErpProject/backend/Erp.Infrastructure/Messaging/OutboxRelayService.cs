using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Outbox;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Messaging;

/// <summary>
/// Replaces Hangfire OutboxProcessorJob as primary transport.
/// Reads unrelayed OutboxMessages, publishes to RabbitMQ, marks as Processed.
/// Runs every 5 seconds as IHostedService (no polling overhead from Hangfire).
///
/// Pattern: Transactional Outbox → RabbitMQ relay
/// Guarantees: at-least-once delivery to RabbitMQ.
/// Hangfire OutboxProcessorJob remains as a fallback safety net but will
/// find no pending messages when this relay is healthy.
/// </summary>
public class OutboxRelayService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<OutboxRelayService> _logger;
    private static readonly TimeSpan PollingInterval = TimeSpan.FromSeconds(5);

    private static readonly Dictionary<string, string> EventRoutingKeys = new()
    {
        ["InvoiceApprovedEvent"] = "invoice.approved",
        ["ExpenseApprovedEvent"] = "expense.approved",
    };

    public OutboxRelayService(IServiceProvider services, ILogger<OutboxRelayService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxRelayService started - relaying to RabbitMQ every {Interval}s",
            PollingInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RelayPendingMessages(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogError(ex, "Error in OutboxRelayService");
            }

            await Task.Delay(PollingInterval, stoppingToken);
        }
    }

    private async Task RelayPendingMessages(CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
        var messageBus = scope.ServiceProvider.GetRequiredService<IMessageBus>();

        // Get pending outbox messages not yet relayed to RabbitMQ
        var messages = await ctx.OutboxMessages
            .Where(m => m.Status == OutboxMessageStatus.Pending && m.RetryCount < 5)
            .OrderBy(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(ct);

        if (!messages.Any()) return;

        int relayed = 0;
        foreach (var message in messages)
        {
            try
            {
                var routingKey = EventRoutingKeys.GetValueOrDefault(
                    message.EventType,
                    message.EventType.ToLowerInvariant().Replace("event", ""));

                await messageBus.PublishAsync(routingKey, message.Payload, message.EventType, ct);

                message.Status = OutboxMessageStatus.Processed;
                message.ProcessedAt = DateTime.UtcNow;
                relayed++;
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.Error = ex.Message;
                if (message.RetryCount >= 5)
                    message.Status = OutboxMessageStatus.Failed;

                _logger.LogWarning(ex, "Failed to relay OutboxMessage {Id} to RabbitMQ (attempt {Retry})",
                    message.Id, message.RetryCount);
            }
        }

        await ctx.SaveChangesAsync(ct);

        if (relayed > 0)
            _logger.LogDebug("Relayed {Count} outbox messages to RabbitMQ", relayed);
    }
}

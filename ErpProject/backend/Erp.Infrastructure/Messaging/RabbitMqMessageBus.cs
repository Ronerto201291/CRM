using Erp.Application.Common.Interfaces;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace Erp.Infrastructure.Messaging;

/// <summary>
/// RabbitMQ implementation of IMessageBus.
/// Uses a topic exchange "erp.events" with routing keys like "invoice.approved", "expense.approved".
/// </summary>
public class RabbitMqMessageBus : IMessageBus
{
    public const string ExchangeName = "erp.events";

    private readonly RabbitMqConnectionFactory _connectionFactory;
    private readonly ILogger<RabbitMqMessageBus> _logger;

    public RabbitMqMessageBus(RabbitMqConnectionFactory connectionFactory, ILogger<RabbitMqMessageBus> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task PublishAsync(string routingKey, string eventPayload, string eventType, CancellationToken ct = default)
    {
        try
        {
            var connection = await _connectionFactory.GetConnectionAsync(ct);
            var channel = await connection.CreateChannelAsync(cancellationToken: ct);

            // Declare exchange (idempotent)
            await channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                cancellationToken: ct);

            var envelope = new MessageEnvelope
            {
                EventType = eventType,
                Payload = eventPayload,
                PublishedAt = DateTime.UtcNow,
                MessageId = Guid.NewGuid().ToString()
            };

            var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(envelope));

            var props = new BasicProperties
            {
                ContentType = "application/json",
                DeliveryMode = DeliveryModes.Persistent,  // survive broker restart
                MessageId = envelope.MessageId,
                Type = eventType,
                Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
            };

            await channel.BasicPublishAsync(
                exchange: ExchangeName,
                routingKey: routingKey,
                mandatory: false,
                basicProperties: props,
                body: body,
                cancellationToken: ct);

            _logger.LogDebug("Published {EventType} to RabbitMQ exchange '{Exchange}' key '{Key}'",
                eventType, ExchangeName, routingKey);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish {EventType} to RabbitMQ", eventType);
            throw;
        }
    }

    public async Task<bool> IsHealthyAsync(CancellationToken ct = default)
    {
        try
        {
            var connection = await _connectionFactory.GetConnectionAsync(ct);
            return connection.IsOpen;
        }
        catch
        {
            return false;
        }
    }
}

public record MessageEnvelope
{
    public string MessageId { get; init; } = string.Empty;
    public string EventType { get; init; } = string.Empty;
    public string Payload { get; init; } = string.Empty;
    public DateTime PublishedAt { get; init; }
}

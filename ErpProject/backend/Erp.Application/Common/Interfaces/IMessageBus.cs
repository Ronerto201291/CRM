namespace Erp.Application.Common.Interfaces;

/// <summary>
/// Abstraction for publishing domain events to a message broker.
/// Implemented by RabbitMqMessageBus in Infrastructure.
/// Application layer publishes via this interface — no RabbitMQ dependency in Application.
/// </summary>
public interface IMessageBus
{
    /// <summary>
    /// Publishes an event to the message broker.
    /// </summary>
    /// <param name="routingKey">e.g., "invoice.approved", "expense.approved"</param>
    /// <param name="eventPayload">JSON-serialized event payload</param>
    /// <param name="eventType">Full type name for deserialization</param>
    Task PublishAsync(string routingKey, string eventPayload, string eventType, CancellationToken ct = default);

    /// <summary>
    /// Checks if the message broker is available.
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct = default);
}

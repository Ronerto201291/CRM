using Erp.Domain.Common;

namespace Erp.Domain.Entities.Outbox;

/// <summary>
/// Outbox pattern: guarantees at-least-once delivery of domain events.
/// Events are persisted in the same transaction as the business operation,
/// then processed asynchronously by OutboxProcessorJob.
/// Prevents silent event loss if the process crashes between publish and handler.
/// </summary>
public class OutboxMessage : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string EventType { get; set; } = string.Empty;      // e.g. "InvoiceApprovedEvent"
    public string Payload { get; set; } = string.Empty;        // JSON serialized event
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ProcessedAt { get; set; }
    public string? Error { get; set; }                         // Last error if processing failed
    public int RetryCount { get; set; }
    public OutboxMessageStatus Status { get; set; } = OutboxMessageStatus.Pending;
}

public enum OutboxMessageStatus
{
    Pending = 0,
    Processed = 1,
    Failed = 2
}

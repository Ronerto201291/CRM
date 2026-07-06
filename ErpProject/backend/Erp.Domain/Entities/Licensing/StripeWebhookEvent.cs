namespace Erp.Domain.Entities.Licensing;

/// <summary>Idempotencia de webhooks Stripe (event.Id único).</summary>
public class StripeWebhookEvent
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

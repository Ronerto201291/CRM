using Erp.Domain.Common;

namespace Erp.Modules.Billing.Domain.Entities;

public class QuoteStatusHistory : BaseEntity
{
    public Guid QuoteId { get; set; }
    public Quote? Quote { get; set; }

    public string? FromStatus { get; set; }
    public string ToStatus { get; set; } = string.Empty;

    public Guid? ChangedById { get; set; }                    // null si cambio automático (expiración)
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;

    public string? Reason { get; set; }                       // motivo de rechazo, etc.

    /// <summary>
    /// JSONB. Para aceptaciones por portal: { ip, userAgent, acceptedFromEmail, timestamp }
    /// Para expiración automática: { triggeredBy: "ExpireQuotesJob" }
    /// </summary>
    public string? Metadata { get; set; }
}

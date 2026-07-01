using Erp.Domain.Common;

namespace Erp.Domain.Entities.Licensing;

public class Subscription : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    public string ActiveModules { get; set; } = "[]"; // JSONB
    public bool IsActive { get; set; } = true;
    public string? StripeSubscriptionId { get; set; }
    public string? StripeStatus { get; set; } = "inactive"; // active, trialing, past_due, canceled
}

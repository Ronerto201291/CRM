using Erp.Domain.Common;

namespace Erp.Domain.Entities.Licensing;

public class Subscription : BaseEntity
{
    public Guid CompanyId { get; set; }
    public string PlanName { get; set; } = string.Empty;
    public DateTime ExpirationDate { get; set; }
    /// <summary>Obsoleto — el gating real usa <see cref="Core.TenantModule"/> (ADR-0014).</summary>
    [Obsolete("Usar TenantModules/PlanModules; se mantiene la columna por compatibilidad de esquema.")]
    public string ActiveModules { get; set; } = "[]";
    public bool IsActive { get; set; } = true;
    public string? StripeSubscriptionId { get; set; }
    public string? StripeStatus { get; set; } = "inactive"; // active, trialing, past_due, canceled
}

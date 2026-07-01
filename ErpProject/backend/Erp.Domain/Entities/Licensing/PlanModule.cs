using Erp.Domain.Common;

namespace Erp.Domain.Entities.Licensing;

/// <summary>
/// Defines which modules are included in a given Plan.
/// Enforces which features each SaaS plan can access.
/// </summary>
public class PlanModule : BaseEntity
{
    public Guid PlanId { get; set; }
    public Plan? Plan { get; set; }
    public string ModuleName { get; set; } = string.Empty;  // "Inventory", "OCR", "PublicApi", etc.
    public bool IsIncluded { get; set; } = true;
}

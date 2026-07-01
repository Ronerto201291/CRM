using Erp.Domain.Common;

namespace Erp.Domain.Entities.Licensing;

/// <summary>
/// Represents a SaaS subscription plan (Free, Starter, Professional, Enterprise).
/// Defines which modules are included and the pricing/limits.
/// </summary>
public class Plan : BaseEntity
{
    public string Name { get; set; } = string.Empty;           // "Free", "Starter", "Professional", "Enterprise"
    public string Description { get; set; } = string.Empty;
    public decimal MonthlyPrice { get; set; }                   // EUR/mes
    public decimal YearlyPrice { get; set; }                    // EUR/año (con descuento)
    public int MaxUsers { get; set; } = 1;
    public int MaxInvoicesPerMonth { get; set; } = 50;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<PlanModule> PlanModules { get; set; } = new List<PlanModule>();
}

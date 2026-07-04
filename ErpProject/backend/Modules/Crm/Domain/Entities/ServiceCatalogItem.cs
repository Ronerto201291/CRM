using Erp.Domain.Common;

namespace Erp.Modules.Crm.Domain.Entities;

/// <summary>
/// Catálogo de servicios propio de cada empresa (análogo a Product de Inventory,
/// pero para servicios sin stock). Varía por empresa: un taller y una gestoría
/// tienen catálogos distintos. Se "elimina" desactivando (IsActive=false), nunca
/// con DELETE real, para no dejar huérfanos los contratos ya firmados que lo referencian.
/// </summary>
public class ServiceCatalogItem : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal DefaultPrice { get; set; }
    public decimal DefaultTaxRate { get; set; }
    /// <summary>"Monthly" | "Quarterly" | "Yearly"</summary>
    public string DefaultPeriodicity { get; set; } = "Monthly";
    public bool IsActive { get; set; } = true;
}

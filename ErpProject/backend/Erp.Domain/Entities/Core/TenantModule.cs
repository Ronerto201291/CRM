using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

/// <summary>
/// Controls Granular Module Activation for each Tenant.
/// </summary>
public class TenantModule : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }

    public string ModuleName { get; set; } = string.Empty; // e.g. "Inventory"
    public bool IsEnabled { get; set; } = false;
}

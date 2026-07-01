using Erp.Domain.Common;

namespace Erp.Modules.Inventory.Domain.Entities;

public class Warehouse : AuditableEntity
{
    public Guid CompanyId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
}

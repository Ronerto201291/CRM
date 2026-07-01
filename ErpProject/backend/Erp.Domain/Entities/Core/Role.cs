using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

public class Role : BaseEntity
{
    public Guid CompanyId { get; set; }
    public Company? Company { get; set; }
    
    public string Name { get; set; } = string.Empty;
    
    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
}

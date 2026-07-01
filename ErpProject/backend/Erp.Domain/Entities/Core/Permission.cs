using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

public class Permission : BaseEntity
{
    // Legacy field kept for backward compatibility
    public string Code { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    // ABAC fields: Resource + Action define a granular permission
    public string Resource { get; set; } = string.Empty;  // "Invoice", "Expense", "Client"
    public string Action { get; set; } = string.Empty;    // "Create", "Read", "Update", "Delete", "Approve", "Lock"

    public ICollection<RolePermission> RolePermissions { get; set; } = new List<RolePermission>();
    public ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
}

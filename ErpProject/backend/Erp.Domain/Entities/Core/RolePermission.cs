namespace Erp.Domain.Entities.Core;

public class RolePermission
{
    // Legacy FK-based role (kept for backward compatibility with existing data)
    public Guid RoleId { get; set; }
    public Role? Role { get; set; }

    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }

    // ABAC: string-based role name for cross-tenant role matching
    // (e.g. "Admin", "Manager", "Contable")
    public string RoleName { get; set; } = string.Empty;
}

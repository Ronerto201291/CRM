using Erp.Domain.Common;

namespace Erp.Domain.Entities.Core;

/// <summary>
/// Per-user ABAC permission overrides. These trump role-based permissions.
/// IsGranted = true  → explicit grant (even if role doesn't have it)
/// IsGranted = false → explicit deny  (even if role does have it)
/// </summary>
public class UserPermission : BaseEntity
{
    public Guid UserId { get; set; }
    public User? User { get; set; }

    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }

    /// <summary>true = grant, false = explicit deny</summary>
    public bool IsGranted { get; set; } = true;

    /// <summary>UserId (as string) of the admin who granted/denied this permission.</summary>
    public string? GrantedBy { get; set; }

    /// <summary>null = permanent; set to expire a temporary permission.</summary>
    public DateTime? ExpiresAt { get; set; }
}

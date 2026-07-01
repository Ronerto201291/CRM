namespace Erp.Application.Common.Interfaces;

/// <summary>
/// ABAC permission service interface.
/// Defined in Application layer; implemented in Infrastructure (PermissionService).
/// Resolution order:
///   1. User-specific explicit DENY  → denied (always wins)
///   2. User-specific explicit GRANT → allowed
///   3. Role-based permission        → allowed if role has it
///   4. Default                      → denied
/// </summary>
public interface IPermissionService
{
    /// <summary>
    /// Checks if the currently authenticated user (resolved from JWT via IHttpContextAccessor)
    /// has permission for the given resource + action combination.
    /// </summary>
    Task<bool> HasPermissionAsync(string resource, string action, CancellationToken ct = default);

    /// <summary>
    /// Checks permission for a specific user ID (used for admin operations).
    /// </summary>
    Task<bool> UserHasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default);

    /// <summary>
    /// Returns all effective permission strings ("Resource:Action") for the current user.
    /// Results are cached in Redis for 5 minutes.
    /// </summary>
    Task<IReadOnlyList<string>> GetUserPermissionsAsync(CancellationToken ct = default);
}

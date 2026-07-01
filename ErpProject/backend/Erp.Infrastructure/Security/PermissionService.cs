using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using System.Security.Claims;
using System.Text.Json;

namespace Erp.Infrastructure.Security;

/// <summary>
/// ABAC permission service. Resolution order:
///   1. User-specific explicit DENY  → denied (regardless of role)
///   2. User-specific explicit GRANT → allowed
///   3. Role-based permission        → allowed if user's role has it
///   4. Default                      → denied
///
/// Results are cached per user in Redis for 5 minutes.
/// Cache is invalidated implicitly by TTL; for immediate invalidation, call
/// IDistributedCache.RemoveAsync("permissions:user:{userId}").
/// </summary>
public class PermissionService : IPermissionService
{
    private readonly Erp.Infrastructure.Data.ErpDbContext _ctx;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDistributedCache _cache;

    public PermissionService(
        Erp.Infrastructure.Data.ErpDbContext ctx,
        IHttpContextAccessor httpContextAccessor,
        IDistributedCache cache)
    {
        _ctx = ctx;
        _httpContextAccessor = httpContextAccessor;
        _cache = cache;
    }

    /// <inheritdoc/>
    public async Task<bool> HasPermissionAsync(string resource, string action, CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return false;
        return await UserHasPermissionAsync(userId.Value, resource, action, ct);
    }

    /// <inheritdoc/>
    public async Task<bool> UserHasPermissionAsync(Guid userId, string resource, string action, CancellationToken ct = default)
    {
        var permissions = await GetUserPermissionsInternalAsync(userId, ct);
        return permissions.Contains($"{resource}:{action}");
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<string>> GetUserPermissionsAsync(CancellationToken ct = default)
    {
        var userId = GetCurrentUserId();
        if (userId == null) return Array.Empty<string>();
        var set = await GetUserPermissionsInternalAsync(userId.Value, ct);
        return set.ToList();
    }

    // ─── Internal ──────────────────────────────────────────────────────────────

    private async Task<HashSet<string>> GetUserPermissionsInternalAsync(Guid userId, CancellationToken ct)
    {
        var cacheKey = $"permissions:user:{userId}";

        var cached = await _cache.GetStringAsync(cacheKey, ct);
        if (cached != null)
            return JsonSerializer.Deserialize<HashSet<string>>(cached) ?? new HashSet<string>();

        var granted = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var denied  = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // ── Step 1: User-specific overrides (highest priority) ─────────────────
        // IgnoreQueryFilters so we don't accidentally filter out global permissions.
        var userPerms = await _ctx.UserPermissions
            .AsNoTracking()
            .Include(up => up.Permission)
            .Where(up => up.UserId == userId
                      && (up.ExpiresAt == null || up.ExpiresAt > DateTime.UtcNow))
            .ToListAsync(ct);

        foreach (var up in userPerms)
        {
            if (up.Permission == null) continue;
            var key = $"{up.Permission.Resource}:{up.Permission.Action}";
            if (up.IsGranted)
                granted.Add(key);
            else
                denied.Add(key);  // explicit deny — wins over everything
        }

        // ── Step 2: Role-based permissions ─────────────────────────────────────
        // User has a single Role (via RoleId FK). We load the role name and
        // look up RolePermissions by RoleName (string-based ABAC approach).
        var user = await _ctx.Users
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user?.Role != null)
        {
            var roleName = user.Role.Name;
            var rolePerms = await _ctx.RolePermissions
                .AsNoTracking()
                .Include(rp => rp.Permission)
                .Where(rp => rp.RoleName == roleName)
                .ToListAsync(ct);

            foreach (var rp in rolePerms)
            {
                if (rp.Permission == null) continue;
                var key = $"{rp.Permission.Resource}:{rp.Permission.Action}";
                // Explicit deny from user-level overrides wins; role grant does not override it
                if (!denied.Contains(key))
                    granted.Add(key);
            }
        }

        // ── Cache for 5 minutes ────────────────────────────────────────────────
        var serialized = JsonSerializer.Serialize(granted);
        await _cache.SetStringAsync(
            cacheKey,
            serialized,
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5)
            },
            ct);

        return granted;
    }

    private Guid? GetCurrentUserId()
    {
        var user = _httpContextAccessor.HttpContext?.User;
        if (user == null) return null;

        var claim = user.FindFirst(ClaimTypes.NameIdentifier)
                 ?? user.FindFirst("sub");

        return claim != null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}

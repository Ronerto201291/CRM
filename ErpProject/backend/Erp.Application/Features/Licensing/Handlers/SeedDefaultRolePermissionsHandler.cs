using Erp.Application.Common.Events;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Application.Features.Licensing.Handlers;

/// <summary>
/// Grants each newly created company's default Admin/Manager/Contable roles a sensible set
/// of ABAC permissions (Permission/RolePermission, ADR-0018 #42c). Without this, retrofitting
/// [RequirePermission] onto controllers would deny every request for every real company,
/// since Permission/RolePermission/UserPermission were never seeded anywhere before this.
///
/// RolePermission.RoleId is a real FK to a specific tenant's Role row (see
/// SeedPermissionCatalog migration), so grants must be seeded per-company against that
/// company's actual Role.Id values — a global seed keyed only by role name is not possible
/// with the current schema.
/// </summary>
public class SeedDefaultRolePermissionsHandler : INotificationHandler<CompanyCreatedEvent>
{
    /// <summary>Resources considered "financial" for the Contable role: full access.
    /// All other resources are granted Read-only (plus Export where it exists) to Contable.</summary>
    private static readonly HashSet<string> FinancialResources = new(StringComparer.Ordinal)
    {
        "Invoice", "Expense", "Quote", "FacturaE", "Accounting", "Budget", "CostCenter",
        "DeferredEntry", "FinancialStatement", "FixedAsset", "Provision", "Vat", "Vies",
        "Recargo", "Prorrata", "BankAccount", "CashSession", "Currency", "Financing",
        "Guarantee", "Consolidation", "Report",
    };

    /// <summary>Resources never granted to Contable (user/role administration).</summary>
    private static readonly HashSet<string> ContableExcludedResources = new(StringComparer.Ordinal)
    {
        "UserManagement",
    };

    /// <summary>Permissions withheld from Manager (Admin-only): user deletion and closing
    /// a fiscal year (irreversible, antifraude-relevant per ADR-0006).</summary>
    private static readonly HashSet<string> ManagerDenyList = new(StringComparer.Ordinal)
    {
        "UserManagement:Delete", "Accounting:Close",
    };

    private readonly IApplicationDbContext _ctx;

    public SeedDefaultRolePermissionsHandler(IApplicationDbContext ctx)
    {
        _ctx = ctx;
    }

    public async Task Handle(CompanyCreatedEvent notification, CancellationToken ct)
    {
        // IgnoreQueryFilters: this runs right after signup, before any tenant context is
        // resolved for the new company, so the CompanyId query filter on Roles would
        // otherwise match nothing and silently no-op (same reasoning as SeedTenantModulesHandler).
        var roles = await _ctx.Roles
            .IgnoreQueryFilters()
            .Where(r => r.CompanyId == notification.CompanyId
                     && (r.Name == "Admin" || r.Name == "Manager" || r.Name == "Contable"))
            .ToListAsync(ct);
        if (roles.Count == 0) return;

        var alreadyGranted = await _ctx.RolePermissions
            .Where(rp => roles.Select(r => r.Id).Contains(rp.RoleId))
            .Select(rp => new { rp.RoleId, rp.PermissionId })
            .ToListAsync(ct);
        var alreadyGrantedSet = alreadyGranted.Select(x => (x.RoleId, x.PermissionId)).ToHashSet();

        var permissions = await _ctx.Permissions.ToListAsync(ct);

        foreach (var role in roles)
        {
            foreach (var permission in permissions)
            {
                if (!IsGrantedByDefault(role.Name, permission.Resource, permission.Code)) continue;
                if (alreadyGrantedSet.Contains((role.Id, permission.Id))) continue;

                _ctx.RolePermissions.Add(new RolePermission
                {
                    RoleId = role.Id,
                    PermissionId = permission.Id,
                    RoleName = role.Name,
                });
            }
        }

        await _ctx.SaveChangesAsync(ct);
    }

    private static bool IsGrantedByDefault(string roleName, string resource, string permissionCode) => roleName switch
    {
        "Admin" => true,
        "Manager" => !ManagerDenyList.Contains(permissionCode),
        "Contable" => !ContableExcludedResources.Contains(resource)
            && (FinancialResources.Contains(resource) || permissionCode.EndsWith(":Read") || permissionCode.EndsWith(":Export")),
        _ => false,
    };
}

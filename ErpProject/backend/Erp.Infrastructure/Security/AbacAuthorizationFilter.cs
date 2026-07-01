using Erp.Application.Common.Attributes;
using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace Erp.Infrastructure.Security;

/// <summary>
/// MVC authorization filter that enforces [RequirePermission] attributes using ABAC.
/// Runs after the existing ModuleAuthorizationFilter (module-level RBAC check).
/// Provides action-level granularity within a module.
///
/// Registration order in Program.cs matters:
///   AddControllers().AddMvcOptions(o => o.Filters.Add&lt;AbacAuthorizationFilter&gt;())
/// This must run AFTER authentication and the ModuleAuthorizationFilter.
/// </summary>
public class AbacAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IPermissionService _permissionService;

    public AbacAuthorizationFilter(IPermissionService permissionService)
    {
        _permissionService = permissionService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var descriptor = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();

        if (descriptor == null) return;

        // Look for [RequirePermission] on the action method first, then the controller class
        var attr = descriptor.MethodInfo.GetCustomAttribute<RequirePermissionAttribute>()
                ?? descriptor.ControllerTypeInfo.GetCustomAttribute<RequirePermissionAttribute>();

        // No ABAC constraint on this endpoint — pass through
        if (attr == null) return;

        // User must be authenticated to reach this point (JWT middleware + [Authorize] handles that)
        if (!context.HttpContext.User.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        var hasPermission = await _permissionService.HasPermissionAsync(attr.Resource, attr.Action);

        if (!hasPermission)
        {
            context.Result = new ObjectResult(new
            {
                error = $"Permiso insuficiente: se requiere '{attr.Resource}:{attr.Action}'",
                code = "PERMISSION_DENIED"
            })
            {
                StatusCode = 403
            };
        }
    }
}

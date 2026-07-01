using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Erp.Api.Security;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string _permissionCode;
    public RequirePermissionAttribute(string permissionCode) => _permissionCode = permissionCode;

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (!user.Identity?.IsAuthenticated ?? true)
        {
            context.Result = new UnauthorizedResult();
            return;
        }
        // In a real implementation, load user permissions from DB/cache and check
        // For now we allow all authenticated users through; this is the extension point
        var permissions = user.Claims.Where(c => c.Type == "Permission").Select(c => c.Value);
        // If permissions are loaded and the required one is missing:
        // if (permissions.Any() && !permissions.Contains(_permissionCode))
        //     context.Result = new ForbidResult();
    }
}

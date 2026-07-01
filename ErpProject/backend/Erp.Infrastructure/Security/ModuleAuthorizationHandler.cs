using Erp.Application.Common.Attributes;
using Erp.Application.Common.Interfaces;
using Erp.Infrastructure.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Erp.Infrastructure.Security;

/// <summary>
/// Authorization handler that validates module access for the current tenant.
/// Checks: (1) tenant exists, (2) active subscription, (3) plan includes module,
/// (4) per-tenant TenantModule override is enabled.
/// </summary>
public class ModuleAuthorizationHandler : AuthorizationHandler<ModuleRequirement>
{
    private readonly ILicensingDbContext _licensingCtx;
    private readonly ITenantContext _tenantContext;

    public ModuleAuthorizationHandler(ILicensingDbContext licensingCtx, ITenantContext tenantContext)
    {
        _licensingCtx = licensingCtx;
        _tenantContext = tenantContext;
    }

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        ModuleRequirement requirement)
    {
        // 1. Tenant must be identified
        if (!_tenantContext.TenantId.HasValue)
        {
            context.Fail();
            return;
        }

        var tenantId = _tenantContext.TenantId.Value;

        // 2. Active, non-expired subscription
        var subscription = await _licensingCtx.Subscriptions
            .AsNoTracking()
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.CompanyId == tenantId && s.IsActive && s.ExpirationDate > DateTime.UtcNow);

        if (subscription == null)
        {
            context.Fail();
            return;
        }

        // 3. Plan includes the requested module
        var plan = await _licensingCtx.Plans
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Include(p => p.PlanModules)
            .FirstOrDefaultAsync(p => p.Name == subscription.PlanName && p.IsActive);

        // Plan not found → deny (cannot grant access to an unknown plan)
        if (plan == null)
        {
            context.Fail();
            return;
        }

        var planAllowsModule = plan.PlanModules
            .Any(pm => pm.ModuleName == requirement.ModuleName && pm.IsIncluded);

        if (!planAllowsModule)
        {
            context.Fail();
            return;
        }

        // 4. Per-tenant module override: TenantModule must be explicitly enabled
        var tenantModuleEnabled = await _licensingCtx.TenantModules
            .AsNoTracking()
            .IgnoreQueryFilters()
            .AnyAsync(m => m.CompanyId == tenantId
                        && m.ModuleName == requirement.ModuleName
                        && m.IsEnabled);

        if (!tenantModuleEnabled)
        {
            context.Fail();
            return;
        }

        context.Succeed(requirement);
    }
}

/// <summary>
/// Requirement that checks if a module is enabled for the current tenant.
/// </summary>
public class ModuleRequirement : IAuthorizationRequirement
{
    public string ModuleName { get; }

    public ModuleRequirement(string moduleName)
    {
        ModuleName = moduleName;
    }
}

/// <summary>
/// Policy provider that automatically creates ModuleRequirement policies
/// for endpoints marked with [RequiredModule].
/// </summary>
public class ModulePolicyProvider : IAuthorizationPolicyProvider
{
    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (policyName.StartsWith("Module:"))
        {
            var moduleName = policyName.Substring("Module:".Length);
            var policy = new AuthorizationPolicyBuilder()
                .AddRequirements(new ModuleRequirement(moduleName))
                .Build();
            return Task.FromResult((AuthorizationPolicy?)policy);
        }

        return Task.FromResult((AuthorizationPolicy?)null);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
    {
        var policy = new AuthorizationPolicyBuilder()
            .RequireAuthenticatedUser()
            .Build();
        return Task.FromResult(policy);
    }

    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
    {
        return Task.FromResult((AuthorizationPolicy?)null);
    }
}

/// <summary>
/// Filter que aplica [RequiredModule] autom�ticamente a los endpoints.
/// </summary>
public class ModuleAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly IAuthorizationService _authorizationService;

    public ModuleAuthorizationFilter(IAuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var endpoint = context.HttpContext.GetEndpoint();
        var controllerAction = endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>();

        if (controllerAction == null)
            return;

        // Buscar atributo en m�todo
        var moduleAttribute = controllerAction.MethodInfo.GetCustomAttribute<RequiredModuleAttribute>()
            ?? controllerAction.ControllerTypeInfo.GetCustomAttribute<RequiredModuleAttribute>();

        if (moduleAttribute == null)
            return;

        // Use requirement directly to avoid needing ModulePolicyProvider registered
        var requirement = new ModuleRequirement(moduleAttribute.ModuleName);
        var result = await _authorizationService.AuthorizeAsync(context.HttpContext.User, null, requirement);

        if (!result.Succeeded)
        {
            context.Result = new ForbidResult();
        }
    }
}

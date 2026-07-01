namespace Erp.Application.Common.Attributes;

/// <summary>
/// Specifies that an endpoint requires a specific module to be enabled for the tenant.
/// Validates TenantModules table before allowing access.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequiredModuleAttribute : Attribute
{
    public string ModuleName { get; }
    
    public RequiredModuleAttribute(string moduleName)
    {
        ModuleName = moduleName;
    }
}

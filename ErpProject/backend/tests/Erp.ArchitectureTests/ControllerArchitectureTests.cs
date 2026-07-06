using System.Reflection;
using Erp.Application.Common.Attributes;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Erp.ArchitectureTests;

/// <summary>ADR-0018 #35 / #42c — reglas arquitectónicas en CI.</summary>
public class ControllerArchitectureTests
{
    private static readonly HashSet<string> ExemptModuleControllers =
    [
        "PublicInvoicesController",
        "PublicInvoiceViewController",
        "PublicQuotesController",
        "PublicSupplierUploadController",
    ];

    [Fact]
    public void ModuleApiControllers_HaveRequiredModuleAttribute()
    {
        var violations = ModuleBusinessControllers()
            .Where(t => t.GetCustomAttribute<RequiredModuleAttribute>() == null)
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "Controllers de módulo deben tener [RequiredModule] a nivel clase (ADR-0018 #42c):\n"
            + string.Join("\n", violations));
    }

    [Fact]
    public void ModuleApiControllers_HaveRequirePermissionOnActions()
    {
        var httpVerbs = new[]
        {
            typeof(HttpGetAttribute), typeof(HttpPostAttribute), typeof(HttpPutAttribute),
            typeof(HttpPatchAttribute), typeof(HttpDeleteAttribute),
        };

        var violations = new List<string>();
        foreach (var controller in ModuleBusinessControllers())
        {
            var classPerm = controller.GetCustomAttribute<RequirePermissionAttribute>();
            foreach (var method in controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                if (!httpVerbs.Any(v => method.GetCustomAttribute(v) != null))
                    continue;

                var methodPerm = method.GetCustomAttribute<RequirePermissionAttribute>();
                if (classPerm == null && methodPerm == null)
                    violations.Add($"{controller.Name}.{method.Name}");
            }
        }

        Assert.True(violations.Count == 0,
            "Acciones HTTP de controllers de módulo deben tener [RequirePermission] (clase o método):\n"
            + string.Join("\n", violations));
    }

    private static IEnumerable<Type> ModuleBusinessControllers() =>
        AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name?.Contains(".Modules.", StringComparison.Ordinal) == true
                     && a.GetName().Name!.EndsWith(".Api", StringComparison.Ordinal))
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => t.Namespace?.Contains(".Api.Controllers", StringComparison.Ordinal) == true)
            .Where(t => t.Namespace is null || !t.Namespace.Contains(".Public.", StringComparison.Ordinal))
            .Where(t => !ExemptModuleControllers.Contains(t.Name))
            .Where(t =>
            {
                var allowAnon = t.GetCustomAttribute<AllowAnonymousAttribute>() != null;
                var authorize = t.GetCustomAttribute<AuthorizeAttribute>() != null;
                return !(allowAnon && !authorize);
            })
            .OrderBy(t => t.FullName);

    [Fact]
    public void ApiControllers_DoNotInjectDbContextDirectly()
    {
        var dbContextTypeNames = new HashSet<string>
        {
            "ErpDbContext", "CrmDbContext", "BillingDbContext", "AccountingDbContext",
            "InventoryDbContext", "TreasuryDbContext", "ExpensesDbContext", "PayrollDbContext",
            "PurchasingDbContext", "SalesDbContext"
        };

        var violations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Erp.") == true)
            .SelectMany(a => SafeGetTypes(a))
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Where(p => dbContextTypeNames.Contains(p.ParameterType.Name))
                .Select(p => $"{t.FullName} inyecta {p.ParameterType.Name}"))
            .ToList();

        Assert.True(violations.Count == 0,
            "Controllers no deben inyectar DbContext directamente:\n" + string.Join("\n", violations));
    }

    [Fact]
    public void DomainAssemblies_DoNotReferenceEntityFrameworkCore()
    {
        var violations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.Contains(".Domain") == true)
            .Where(a => a.GetReferencedAssemblies().Any(r => r.Name == "Microsoft.EntityFrameworkCore"))
            .Select(a => a.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "Domain no debe referenciar EF Core:\n" + string.Join("\n", violations));
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }

    // La comprobación de "Erp.Infrastructure no debe referenciar Modules/*/Application"
    // (ADR-0018 #13) vive ahora en DependencyDirectionArchitectureTests, ampliada a los
    // tres ensamblados core y reforzada con una comprobación adicional a nivel de .csproj
    // — ver ese archivo para el porqué (regresión real detectada en contra-auditoría).
}

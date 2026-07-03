using System.Reflection;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace Erp.ArchitectureTests;

/// <summary>ADR-0018 #35 — reglas arquitectónicas en CI.</summary>
public class ControllerArchitectureTests
{
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
}

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
            "PurchasingDbContext", "SalesDbContext",
            "IApplicationDbContext", "ICrmDbContext", "IBillingDbContext", "IAccountingDbContext",
            "IInventoryDbContext", "ITreasuryDbContext", "IExpensesDbContext", "IPayrollDbContext",
            "IPurchasingDbContext", "ISalesDbContext",
        };

        var exempt = new HashSet<string>
        {
            "Erp.Api.Controllers.FiscalHomologationController",
            "Erp.Api.Controllers.Subscriptions.StripeWebhookController",
        };

        var violations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Erp.") == true)
            .SelectMany(a => SafeGetTypes(a))
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => !exempt.Contains(t.FullName ?? ""))
            .SelectMany(t => t.GetConstructors()
                .SelectMany(c => c.GetParameters())
                .Where(p => dbContextTypeNames.Contains(p.ParameterType.Name)
                         || p.ParameterType.Name.EndsWith("DbContext", StringComparison.Ordinal))
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

    [Fact]
    public void ApiControllers_UseMediatorOrSender()
    {
        var exempt = new HashSet<string>
        {
            // Metadatos / webhooks sin pipeline CQRS
            "Erp.Api.Controllers.FiscalHomologationController",
            "Erp.Api.Controllers.Subscriptions.StripeWebhookController",
        };

        var violations = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.FullName?.StartsWith("Erp.") == true)
            .SelectMany(a => SafeGetTypes(a))
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .Where(t => !exempt.Contains(t.FullName ?? ""))
            .Where(t => !t.GetConstructors()
                .Any(c => c.GetParameters().Any(p =>
                    p.ParameterType.Name is "IMediator" or "ISender")))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "Controllers deben inyectar IMediator/ISender:\n" + string.Join("\n", violations));
    }

    /// <summary>Controllers delgados: sin parseo inline ni búsquedas en listas (ADR-0018).</summary>
    [Fact]
    public void ApiControllers_DoNotContainInlineParsingOrListFiltering()
    {
        var repoRoot = FindRepoRoot();
        var backendRoot = Path.Combine(repoRoot, "ErpProject", "backend");
        if (!Directory.Exists(backendRoot))
            backendRoot = Path.Combine(repoRoot, "backend");

        var exemptFiles = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "StripeWebhookController.cs", // lectura de header HTTP
        };

        var parsePattern = new System.Text.RegularExpressions.Regex(
            @"\b(DateTime|int|long|Guid|decimal|double|float|bool)\.Parse\s*\(",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var listFilterPattern = new System.Text.RegularExpressions.Regex(
            @"\.(FirstOrDefault|SingleOrDefault|Where)\s*\(",
            System.Text.RegularExpressions.RegexOptions.Compiled);

        var violations = new List<string>();

        foreach (var file in Directory.EnumerateFiles(backendRoot, "*Controller.cs", SearchOption.AllDirectories))
        {
            var fileName = Path.GetFileName(file);
            if (exemptFiles.Contains(fileName))
                continue;

            var lines = File.ReadAllLines(file);
            for (var i = 0; i < lines.Length; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith("//") || trimmed.StartsWith("///"))
                    continue;

                if (parsePattern.IsMatch(trimmed))
                    violations.Add($"{RelativePath(backendRoot, file)}:{i + 1}: parseo inline — {trimmed}");

                if (listFilterPattern.IsMatch(trimmed))
                    violations.Add($"{RelativePath(backendRoot, file)}:{i + 1}: filtrado en controller — {trimmed}");
            }
        }

        Assert.True(violations.Count == 0,
            "Controllers no deben parsear ni filtrar datos inline:\n" + string.Join("\n", violations));
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            if (Directory.Exists(Path.Combine(dir, "ErpProject", "backend"))
                || Directory.Exists(Path.Combine(dir, "backend")))
                return dir;
            dir = Directory.GetParent(dir)?.FullName ?? string.Empty;
        }
        throw new InvalidOperationException("No se encontró la raíz del repo para escanear controllers.");
    }

    private static string RelativePath(string root, string fullPath)
        => Path.GetRelativePath(root, fullPath).Replace('\\', '/');

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}

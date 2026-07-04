using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Erp.IntegrationTests;

/// <summary>
/// Descubre un endpoint representativo por controller para smoke tests HTTP (ADR-0018 #32).
/// </summary>
public sealed record ControllerProbe(string ControllerName, string HttpMethod, string Path, bool ExpectUnauthorized);

public static class ControllerEndpointDiscovery
{
    private static readonly HashSet<string> SkipControllers =
    [
        "StripeWebhookController",
        "PublicQuotesController",
        "PublicInvoicesController",
        "PublicApiController",
    ];

    static ControllerEndpointDiscovery()
    {
        _ = typeof(Erp.Api.Controllers.AuthController).Assembly;
        _ = typeof(Erp.Modules.Crm.Api.Controllers.ClientsController).Assembly;
        _ = typeof(Erp.Modules.Billing.Api.Controllers.InvoicesController).Assembly;
        _ = typeof(Erp.Modules.Accounting.Api.Controllers.AccountingController).Assembly;
        _ = typeof(Erp.Modules.Inventory.Api.Controllers.ProductsController).Assembly;
        _ = typeof(Erp.Modules.Expenses.Api.Controllers.ExpensesController).Assembly;
        _ = typeof(Erp.Modules.Treasury.Api.Controllers.TreasuryController).Assembly;
        _ = typeof(Erp.Modules.Payroll.Api.Controllers.PayrollController).Assembly;
        _ = typeof(Erp.Modules.Purchasing.Api.Controllers.PurchaseOrdersController).Assembly;
        _ = typeof(Erp.Modules.Sales.Api.Controllers.SalesOrdersController).Assembly;
    }

    public static IReadOnlyList<ControllerProbe> DiscoverOneEndpointPerController()
    {
        var probes = new List<ControllerProbe>();

        var controllers = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => !a.IsDynamic && a.GetName().Name?.StartsWith("Erp.") == true)
            .SelectMany(SafeGetTypes)
            .Where(t => t.IsClass && !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t))
            .GroupBy(t => t.FullName!)
            .Select(g => g.First())
            .OrderBy(t => t.FullName)
            .ToList();

        foreach (var controller in controllers)
        {
            if (SkipControllers.Contains(controller.Name))
                continue;

            if (controller.GetCustomAttribute<AllowAnonymousAttribute>() != null
                && controller.GetCustomAttribute<AuthorizeAttribute>() == null)
                continue;

            var classRoute = controller.GetCustomAttribute<RouteAttribute>()?.Template ?? "";
            var classAuthorize = controller.GetCustomAttribute<AuthorizeAttribute>() != null;

            var (method, httpMethod) = PickEndpoint(controller);
            if (method == null)
                continue;

            if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                continue;

            var methodAuthorize = method.GetCustomAttribute<AuthorizeAttribute>() != null;
            var requiresAuth = classAuthorize || methodAuthorize;
            var path = BuildPath(classRoute, GetMethodRouteTemplate(method), controller.Name);

            probes.Add(new ControllerProbe(controller.Name, httpMethod, path, requiresAuth));
        }

        return probes;
    }

    private static string GetMethodRouteTemplate(MethodInfo method)
        => method.GetCustomAttribute<HttpGetAttribute>()?.Template
           ?? method.GetCustomAttribute<HttpPostAttribute>()?.Template
           ?? "";

    private static (MethodInfo? Method, string HttpMethod) PickEndpoint(Type controller)
    {
        var methods = controller.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly)
            .Where(m => m.GetCustomAttribute<NonActionAttribute>() == null)
            .ToList();

        foreach (var method in methods)
        {
            if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                continue;
            if (method.GetCustomAttribute<HttpGetAttribute>() != null)
                return (method, "GET");
        }

        foreach (var method in methods)
        {
            if (method.GetCustomAttribute<AllowAnonymousAttribute>() != null)
                continue;
            if (method.GetCustomAttribute<HttpPostAttribute>() != null)
                return (method, "POST");
        }

        return (null, "GET");
    }

    private static string BuildPath(string classRoute, string methodRoute, string controllerName)
    {
        static string Combine(string a, string b)
        {
            a = a.Trim('/');
            b = b.Trim('/');
            if (string.IsNullOrEmpty(a)) return b;
            if (string.IsNullOrEmpty(b)) return a;
            return $"{a}/{b}";
        }

        var controllerToken = controllerName.Replace("Controller", "", StringComparison.Ordinal);
        var route = Combine(classRoute, methodRoute);
        route = route.Replace("[controller]", controllerToken, StringComparison.OrdinalIgnoreCase);
        route = Regex.Replace(route, @"\{version:apiVersion\}", "1");
        route = Regex.Replace(route, @"\{(\w+):guid\}", "00000000-0000-0000-0000-000000000001");
        route = Regex.Replace(route, @"\{(\w+):int\}", "1");
        route = Regex.Replace(route, @"\{(\w+)\}", "test");

        if (!route.StartsWith('/'))
            route = "/" + route;

        return route;
    }

    private static IEnumerable<Type> SafeGetTypes(Assembly assembly)
    {
        try { return assembly.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { return ex.Types.Where(t => t != null)!; }
    }
}

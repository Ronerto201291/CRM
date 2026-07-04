using System.Net;
using System.Net.Http.Headers;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Core;
using Erp.Domain.Entities.Licensing;
using Erp.Infrastructure.Data;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Inventory.Infrastructure.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Verifica en vivo, contra el host real de ASP.NET Core (JWT auth real, tenant
/// resolution real, filtros MVC reales), que [RequiredModule] realmente bloquea un
/// endpoint cuando el módulo no está licenciado para el tenant — el bug concreto que
/// tenía ModuleAuthorizationFilter (registrado solo como interfaz, nunca como tipo
/// concreto, ver ADR-0018 #42c). Usa EF InMemory en vez de Postgres real porque
/// Testcontainers no puede descargar imágenes de Docker Hub en este sandbox (egress
/// bloqueado) — swap de DbContext, no de lógica de autorización.
/// </summary>
public class ModuleAuthorizationEndToEndTests : IClassFixture<InMemoryErpWebApplicationFactory>
{
    private readonly InMemoryErpWebApplicationFactory _factory;

    public ModuleAuthorizationEndToEndTests(InMemoryErpWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task RequiredModule_WhenPlanDoesNotIncludeModule_Returns403()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var (companyId, userId) = await SeedCompanyAsync(ctx, includeInventoryModule: false, enableTenantModule: false);
        var token = GenerateToken(scope, userId, companyId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", companyId.ToString());

        var response = await client.GetAsync("/api/inventory/products");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task RequiredModule_WhenPlanIncludesModuleAndPermissionGranted_DoesNotReturn403()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        var (companyId, userId) = await SeedCompanyAsync(ctx, includeInventoryModule: true, enableTenantModule: true);
        await GrantProductReadPermissionAsync(ctx, companyId);
        var token = GenerateToken(scope, userId, companyId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", companyId.ToString());

        var response = await client.GetAsync("/api/inventory/products");

        Assert.NotEqual(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.NotEqual(HttpStatusCode.Unauthorized, response.StatusCode);
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task RequiredModule_WhenModuleLicensedButPermissionMissing_Returns403WithPermissionDenied()
    {
        using var scope = _factory.Services.CreateScope();
        var ctx = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

        // Module licensed and enabled, but no RolePermission grant for Product:Read.
        var (companyId, userId) = await SeedCompanyAsync(ctx, includeInventoryModule: true, enableTenantModule: true);
        var token = GenerateToken(scope, userId, companyId);

        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add("X-Tenant-Id", companyId.ToString());

        var response = await client.GetAsync("/api/inventory/products");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("PERMISSION_DENIED", body);
    }

    private static async Task<(Guid companyId, Guid userId)> SeedCompanyAsync(
        ErpDbContext ctx, bool includeInventoryModule, bool enableTenantModule)
    {
        var companyId = Guid.NewGuid();
        var planId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        ctx.Companies.Add(new Company
        {
            Id = companyId, Name = "E2E Test SL", TaxId = $"B{Guid.NewGuid():N}"[..9],
            IsActive = true, Country = "ES",
        });
        ctx.Plans.Add(new Plan { Id = planId, Name = $"Plan-{companyId}", Description = "e2e", IsActive = true });
        ctx.PlanModules.Add(new PlanModule
        {
            Id = Guid.NewGuid(), PlanId = planId, ModuleName = "Inventory", IsIncluded = includeInventoryModule,
        });
        ctx.Subscriptions.Add(new Subscription
        {
            Id = Guid.NewGuid(), CompanyId = companyId, PlanName = $"Plan-{companyId}",
            IsActive = true, ExpirationDate = DateTime.UtcNow.AddYears(1), StripeStatus = "active",
        });
        if (enableTenantModule)
        {
            ctx.TenantModules.Add(new TenantModule
            {
                Id = Guid.NewGuid(), CompanyId = companyId, ModuleName = "Inventory", IsEnabled = true,
            });
        }
        ctx.Roles.Add(new Role { Id = roleId, CompanyId = companyId, Name = "Admin" });
        ctx.Users.Add(new User
        {
            Id = userId, CompanyId = companyId, Email = $"{userId}@e2e.test",
            FirstName = "E2E", LastName = "User", IsActive = true, RoleId = roleId,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("irrelevant", workFactor: 4),
        });
        ctx.UserCompanies.Add(new UserCompany
        {
            Id = Guid.NewGuid(), UserId = userId, CompanyId = companyId, RoleId = roleId, IsDefault = true,
        });

        await ctx.SaveChangesAsync();
        return (companyId, userId);
    }

    private static async Task GrantProductReadPermissionAsync(ErpDbContext ctx, Guid companyId)
    {
        var role = await ctx.Roles.IgnoreQueryFilters()
            .SingleAsync(r => r.CompanyId == companyId && r.Name == "Admin");
        var permission = new Permission
        {
            Id = Guid.NewGuid(), Resource = "Product", Action = "Read", Code = "Product:Read", Description = "e2e",
        };
        ctx.Permissions.Add(permission);
        ctx.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = permission.Id, RoleName = "Admin" });
        await ctx.SaveChangesAsync();
    }

    private static string GenerateToken(IServiceScope scope, Guid userId, Guid companyId)
    {
        var jwtProvider = scope.ServiceProvider.GetRequiredService<IJwtProvider>();
        var user = new User { Id = userId, CompanyId = companyId, Email = "irrelevant@e2e.test" };
        return jwtProvider.Generate(user, companyId);
    }
}

/// <summary>
/// Host de integración con EF InMemory en vez de Postgres real, para poder ejercitar
/// el pipeline HTTP completo (auth JWT, resolución de tenant, filtros MVC) sin
/// depender de Testcontainers (bloqueado por egress en este sandbox).
/// </summary>
public class InMemoryErpWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _dbSuffix = Guid.NewGuid().ToString("N");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile("appsettings.IntegrationTests.json", optional: true);
        });

        builder.ConfigureServices(services =>
        {
            ReplaceWithInMemory<ErpDbContext>(services, $"e2e-core-{_dbSuffix}");
            ReplaceWithInMemory<InventoryDbContext>(services, $"e2e-inventory-{_dbSuffix}");
            ReplaceWithInMemory<BillingDbContext>(services, $"e2e-billing-{_dbSuffix}");
        });
    }

    private static readonly IServiceProvider InMemoryProvider =
        new ServiceCollection().AddEntityFrameworkInMemoryDatabase().BuildServiceProvider();

    private static void ReplaceWithInMemory<TContext>(IServiceCollection services, string dbName)
        where TContext : DbContext
    {
        var descriptor = services.SingleOrDefault(d => d.ServiceType == typeof(DbContextOptions<TContext>));
        if (descriptor != null) services.Remove(descriptor);

        // UseInternalServiceProvider: the app's Npgsql-backed DbContexts (Billing, Crm,
        // Accounting...) stay registered untouched. Without a dedicated internal service
        // provider, EF's provider-conflict check ("only a single database provider can be
        // registered") fires because Npgsql and InMemory both end up visible from the same
        // root container.
        services.AddDbContext<TContext>(options =>
            options.UseInMemoryDatabase(dbName).UseInternalServiceProvider(InMemoryProvider));
    }
}

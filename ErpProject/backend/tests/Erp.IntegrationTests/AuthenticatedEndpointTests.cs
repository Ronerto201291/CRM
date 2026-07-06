using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Flujos HTTP autenticados con JWT real (register → token → 200).
/// </summary>
public class AuthenticatedEndpointTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public AuthenticatedEndpointTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_ThenGetClients_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var auth = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"admin-{Guid.NewGuid():N}"[..18]);
        Assert.False(string.IsNullOrEmpty(auth.Token));

        var companiesResponse = await auth.Client.GetAsync("/api/auth/companies");
        Assert.Equal(HttpStatusCode.OK, companiesResponse.StatusCode);

        var clientsResponse = await auth.Client.GetAsync("/api/clients");
        Assert.True(
            clientsResponse.StatusCode == HttpStatusCode.OK,
            $"GET /api/clients → {clientsResponse.StatusCode}: {await clientsResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Register_ThenGetInvoices_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var auth = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"billing-{Guid.NewGuid():N}"[..18]);
        var invoicesResponse = await auth.Client.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.OK, invoicesResponse.StatusCode);
    }

    [Fact]
    public async Task Register_ThenGetSuppliers_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var auth = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"suppliers-{Guid.NewGuid():N}"[..18]);
        var suppliersResponse = await auth.Client.GetAsync("/api/suppliers");
        Assert.Equal(HttpStatusCode.OK, suppliersResponse.StatusCode);
    }

    [Fact]
    public async Task TestAuthHelper_GeneratesValidToken_AcceptedByApi()
    {
        if (!_factory.DockerAvailable)
            return;

        var registerBody = await IntegrationTestAuth.RegisterFreeAsync(_factory, $"jwt-{Guid.NewGuid():N}"[..18]);
        await IntegrationTestModuleHelper.UpgradeToEnterpriseAndEnableAllModulesAsync(
            _factory.GetConnectionString(), registerBody.CompanyId);

        var manualToken = TestAuthHelper.CreateToken(
            registerBody.UserId,
            registerBody.Email,
            registerBody.CompanyId);

        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, manualToken, registerBody.CompanyId);

        var response = await authedClient.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

public class MultiTenantIsolationIntegrationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public MultiTenantIsolationIntegrationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantA_CannotSeeTenantB_Clients()
    {
        if (!_factory.DockerAvailable)
            return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantA = await IntegrationTestAuth.RegisterFreeAsync(_factory, $"a-{suffix}", $"A-{suffix}");
        var tenantB = await IntegrationTestAuth.RegisterFreeAsync(_factory, $"b-{suffix}", $"B-{suffix}");

        var clientA = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(clientA, tenantA.Token, tenantA.CompanyId);

        var createResponse = await clientA.PostAsJsonAsync("/api/clients", new
        {
            name = "Cliente exclusivo A",
            taxId = "12345678Z",
            email = "cliente-a@test.local",
            customFields = "{}",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var clientB = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(clientB, tenantB.Token, tenantB.CompanyId);

        var listB = await clientB.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.OK, listB.StatusCode);

        var json = await listB.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : doc.RootElement;

        if (items.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in items.EnumerateArray())
            {
                if (item.TryGetProperty("name", out var nameProp))
                    Assert.NotEqual("Cliente exclusivo A", nameProp.GetString());
            }
        }
    }
}

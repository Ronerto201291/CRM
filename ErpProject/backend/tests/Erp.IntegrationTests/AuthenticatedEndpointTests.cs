using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
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

        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Test Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"admin-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);
        Assert.False(string.IsNullOrEmpty(registerBody!.Token));

        TestAuthHelper.ApplyAuth(client, registerBody.Token, registerBody.CompanyId);

        var companiesResponse = await client.GetAsync("/api/auth/companies");
        Assert.Equal(HttpStatusCode.OK, companiesResponse.StatusCode);

        var clientsResponse = await client.GetAsync("/api/clients");
        Assert.True(
            clientsResponse.StatusCode == HttpStatusCode.OK,
            $"GET /api/clients → {clientsResponse.StatusCode}: {await clientsResponse.Content.ReadAsStringAsync()}");
    }

    [Fact]
    public async Task Register_ThenGetInvoices_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Billing {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"billing-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        TestAuthHelper.ApplyAuth(client, registerBody!.Token, registerBody.CompanyId);

        var invoicesResponse = await client.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.OK, invoicesResponse.StatusCode);
    }

    [Fact]
    public async Task Register_ThenGetSuppliers_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"CRM Suppliers {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"suppliers-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        TestAuthHelper.ApplyAuth(client, registerBody!.Token, registerBody.CompanyId);

        var suppliersResponse = await client.GetAsync("/api/suppliers");
        Assert.Equal(HttpStatusCode.OK, suppliersResponse.StatusCode);
    }

    [Fact]
    public async Task TestAuthHelper_GeneratesValidToken_AcceptedByApi()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"JWT {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"jwt-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        var manualToken = TestAuthHelper.CreateToken(
            registerBody!.UserId,
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
        var tenantA = await RegisterTenantAsync($"A-{suffix}", $"a-{suffix}@test.local");
        var tenantB = await RegisterTenantAsync($"B-{suffix}", $"b-{suffix}@test.local");

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

    private async Task<RegisterCompanyResponse> RegisterTenantAsync(string companyName, string email)
    {
        var client = _factory.CreatePostgresClient();
        var response = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = companyName,
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = email,
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        return body!;
    }
}

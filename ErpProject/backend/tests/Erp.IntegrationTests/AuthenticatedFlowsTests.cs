using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Flujos CRUD autenticados end-to-end (register → JWT → crear → listar).
/// </summary>
public class AuthenticatedFlowsTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public AuthenticatedFlowsTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Register_CreateClient_ThenListContainsClient()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"flow-client-{Guid.NewGuid():N}"[..20]);

        var createResponse = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "Cliente integración",
            taxId = "12345678Z",
            email = "integracion@test.local",
            customFields = "{}",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/clients");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : doc.RootElement;

        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(items.EnumerateArray(), el =>
            el.TryGetProperty("name", out var n) && n.GetString() == "Cliente integración");
    }

    [Fact]
    public async Task Register_CreateDraftInvoice_ThenListContainsInvoice()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"flow-inv-{Guid.NewGuid():N}"[..20]);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente factura",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio test",
                    quantity = 1,
                    unitPrice = 100,
                    taxRate = 21,
                    surchargeRate = 0,
                    tipoOperacion = "Nacional",
                },
            },
        });

        Assert.True(
            createResponse.StatusCode == HttpStatusCode.Created,
            $"POST /api/invoices → {createResponse.StatusCode}: {await createResponse.Content.ReadAsStringAsync()}");

        var listResponse = await client.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var json = await listResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var items = doc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : doc.RootElement;

        Assert.True(items.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task Register_GetFiscalCalendar_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"flow-fiscal-{Guid.NewGuid():N}"[..20]);
        var response = await client.GetAsync("/api/fiscal/calendar?year=2026");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Flow Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"{emailPrefix}-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, registerBody!.Token, registerBody.CompanyId);
        return authedClient;
    }
}

public class MultiTenantInvoiceIsolationTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public MultiTenantInvoiceIsolationTests(PostgresWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantA_CannotSeeTenantB_Invoices()
    {
        if (!_factory.DockerAvailable)
            return;

        var suffix = Guid.NewGuid().ToString("N")[..8];
        var tenantA = await RegisterTenantAsync($"InvA-{suffix}", $"inva-{suffix}@test.local");
        var tenantB = await RegisterTenantAsync($"InvB-{suffix}", $"invb-{suffix}@test.local");

        var clientA = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(clientA, tenantA.Token, tenantA.CompanyId);

        var createResponse = await clientA.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Factura exclusiva A",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Línea A",
                    quantity = 1,
                    unitPrice = 50,
                    taxRate = 21,
                    surchargeRate = 0,
                    tipoOperacion = "Nacional",
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var clientB = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(clientB, tenantB.Token, tenantB.CompanyId);

        var listB = await clientB.GetAsync("/api/invoices");
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
                if (item.TryGetProperty("clientName", out var nameProp))
                    Assert.NotEqual("Factura exclusiva A", nameProp.GetString());
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

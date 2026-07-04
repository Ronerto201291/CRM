using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 13: HTTP integración subscription, contacts, permissions, company, facturae smoke.
/// </summary>
public class Phase13IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase13IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Subscription_GetPlansAndCurrent_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"sub-p13-{Guid.NewGuid():N}"[..18]);

        var plans = await client.GetAsync("/api/subscription/plans");
        Assert.Equal(HttpStatusCode.OK, plans.StatusCode);

        var current = await client.GetAsync("/api/subscription");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var body = await current.Content.ReadAsStringAsync();
        Assert.Contains("Free", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Contacts_Crud_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"contacts-p13-{Guid.NewGuid():N}"[..18]);

        var create = await client.PostAsJsonAsync("/api/contacts", new
        {
            name = "Contacto P13",
            email = "contact-p13@test.local",
            phone = "600222333",
            position = "Compras",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var contactId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/contacts?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var getById = await client.GetAsync($"/api/contacts/{contactId}");
        Assert.Equal(HttpStatusCode.OK, getById.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/contacts/{contactId}", new
        {
            name = "Contacto P13 actualizado",
            email = "updated@test.local",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task Permissions_MyPermissions_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"perms-p13-{Guid.NewGuid():N}"[..18]);

        var mine = await client.GetAsync("/api/permissions/my");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
    }

    [Fact]
    public async Task Company_GetAndUpdate_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"company-p13-{Guid.NewGuid():N}"[..18]);

        var get = await client.GetAsync("/api/company");
        Assert.Equal(HttpStatusCode.OK, get.StatusCode);

        var update = await client.PutAsJsonAsync("/api/company", new
        {
            name = "Empresa P13 actualizada",
            address = "Av. Integración 1",
            qrUploadEnabled = true,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task TenantModules_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"modules-p13-{Guid.NewGuid():N}"[..18]);

        var response = await client.GetAsync("/api/tenant/modules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FacturaE_ValidateUnknownInvoice_Returns404()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"facturae-p13-{Guid.NewGuid():N}"[..18]);

        var response = await client.GetAsync($"/api/v1/billing/facturae/{Guid.NewGuid()}/validate");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Phase13 Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"{emailPrefix}-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();

        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, body!.Token, body.CompanyId);

        var keyResponse = await authedClient.PostAsJsonAsync("/api/apikeys", new Erp.Application.Features.ApiKeys.Commands.CreateApiKeyCommand
        {
            Name = "Phase13 Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return (authedClient, body.CompanyId);
    }
}

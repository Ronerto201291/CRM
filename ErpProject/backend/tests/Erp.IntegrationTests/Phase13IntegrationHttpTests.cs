using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        var registerBody = await IntegrationTestAuth.RegisterFreeAsync(_factory, $"sub-p13-{Guid.NewGuid():N}"[..18]);
        var client = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(client, registerBody.Token, registerBody.CompanyId);

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

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"contacts-p13-{Guid.NewGuid():N}"[..18], withApiKey: true);

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

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"perms-p13-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var mine = await client.GetAsync("/api/permissions/my");
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
    }

    [Fact]
    public async Task Company_GetAndUpdate_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"company-p13-{Guid.NewGuid():N}"[..18], withApiKey: true);

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

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"modules-p13-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var response = await client.GetAsync("/api/tenant/modules");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task FacturaE_ValidateUnknownInvoice_Returns404()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"facturae-p13-{Guid.NewGuid():N}"[..18], withApiKey: true);

        var response = await client.GetAsync($"/api/v1/billing/facturae/{Guid.NewGuid()}/validate");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

}

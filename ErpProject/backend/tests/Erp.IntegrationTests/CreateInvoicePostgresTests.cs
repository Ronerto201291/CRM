using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Happy-path CreateInvoiceHandler vía POST /api/invoices (advisory lock Postgres).
/// </summary>
public class CreateInvoicePostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public CreateInvoicePostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PostInvoice_Returns201_WithDraftInvoiceAndCorrelativeNumber()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"create-inv-{Guid.NewGuid():N}"[..20]);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente Postgres",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio advisory lock",
                    quantity = 1,
                    unitPrice = 100,
                    taxRate = 21,
                    surchargeRate = 0,
                    tipoOperacion = "Nacional",
                },
            },
        });

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = created.GetProperty("id").GetGuid();
        Assert.Equal("Draft", created.GetProperty("status").GetString());
        Assert.Equal(121m, created.GetProperty("total").GetDecimal());
        Assert.Contains("-", created.GetProperty("number").GetString());

        var listResponse = await client.GetAsync("/api/invoices");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var listJson = await listResponse.Content.ReadAsStringAsync();
        using var listDoc = JsonDocument.Parse(listJson);
        var items = listDoc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : listDoc.RootElement;
        Assert.Contains(items.EnumerateArray(), el =>
            el.TryGetProperty("id", out var idProp) && idProp.GetGuid() == invoiceId);
    }

    [Fact]
    public async Task PostInvoice_ThenGetById_Returns200WithSameInvoice()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"get-inv-{Guid.NewGuid():N}"[..18]);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente GET by id",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio GET",
                    quantity = 1,
                    unitPrice = 200,
                    taxRate = 21,
                    surchargeRate = 0,
                    tipoOperacion = "Nacional",
                },
            },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = created.GetProperty("id").GetGuid();

        var getResponse = await client.GetAsync($"/api/invoices/{invoiceId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        var detail = await getResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(invoiceId, detail.GetProperty("id").GetGuid());
        Assert.Equal("Draft", detail.GetProperty("status").GetString());
        Assert.Equal(242m, detail.GetProperty("total").GetDecimal());
        Assert.Equal("Cliente GET by id", detail.GetProperty("clientName").GetString());
    }

    [Fact]
    public async Task GetInvoiceById_NotFound_Returns404()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"get-inv-404-{Guid.NewGuid():N}"[..18]);
        var response = await client.GetAsync($"/api/invoices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<HttpClient> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Invoice Co {suffix}",
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
        return authedClient;
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"create-inv-{Guid.NewGuid():N}"[..20])).Client;

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

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"get-inv-{Guid.NewGuid():N}"[..18])).Client;

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

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(_factory, $"get-inv-404-{Guid.NewGuid():N}"[..18])).Client;
        var response = await client.GetAsync($"/api/invoices/{Guid.NewGuid()}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task PostInvoice_WithUsdCurrency_PopulatesMultiCurrencyFields()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"usd-inv-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;

        // ECB-style rates: ExchangeRate = foreign per 1 EUR (1 EUR ≈ 1.087 USD → 1 USD ≈ 0.92 EUR)
        var eurCurrency = await client.PostAsJsonAsync("/api/v1/treasury/currencies", new
        {
            code = "EUR",
            name = "Euro",
            exchangeRate = 1m,
        });
        Assert.Equal(HttpStatusCode.Created, eurCurrency.StatusCode);

        var usdCurrency = await client.PostAsJsonAsync("/api/v1/treasury/currencies", new
        {
            code = "USD",
            name = "US Dollar",
            exchangeRate = 1.08695652m,
        });
        Assert.Equal(HttpStatusCode.Created, usdCurrency.StatusCode);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente USD",
            clientTaxId = "12345678Z",
            series = "A",
            currencyCode = "USD",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio USD",
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
        Assert.Equal("USD", created.GetProperty("currencyCode").GetString());
        Assert.Equal(121m, created.GetProperty("total").GetDecimal());
        Assert.InRange(created.GetProperty("exchangeRateToEur").GetDecimal(), 0.919m, 0.921m);
        Assert.InRange(created.GetProperty("totalEur").GetDecimal(), 111.30m, 111.34m);
    }

}

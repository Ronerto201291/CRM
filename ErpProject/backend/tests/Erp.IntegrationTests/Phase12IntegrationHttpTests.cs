using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 12: HTTP integración reportes contables, export libro diario, CRM supplier, inventory warehouses, treasury payment orders.
/// </summary>
public class Phase12IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase12IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task AccountingReports_GetDiarioBalancePyg_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"acct-rep-{Guid.NewGuid():N}"[..18]);

        var inicio = "2026-01-01T00:00:00Z";
        var fin = "2026-12-31T23:59:59Z";

        var diario = await client.GetAsync($"/api/reports/diario?FechaInicio={Uri.EscapeDataString(inicio)}&FechaFin={Uri.EscapeDataString(fin)}");
        var diarioBody = await diario.Content.ReadAsStringAsync();
        Assert.True(diario.StatusCode == HttpStatusCode.OK, $"diario → {diario.StatusCode}: {diarioBody}");

        var balance = await client.GetAsync($"/api/reports/balance?FechaCorte={Uri.EscapeDataString(fin)}");
        var balanceBody = await balance.Content.ReadAsStringAsync();
        Assert.True(balance.StatusCode == HttpStatusCode.OK, $"balance → {balance.StatusCode}: {balanceBody}");

        var pyg = await client.GetAsync($"/api/reports/pyg?FechaInicio={Uri.EscapeDataString(inicio)}&FechaFin={Uri.EscapeDataString(fin)}");
        var pygBody = await pyg.Content.ReadAsStringAsync();
        Assert.True(pyg.StatusCode == HttpStatusCode.OK, $"pyg → {pyg.StatusCode}: {pygBody}");
    }

    [Fact]
    public async Task AccountingController_TrialBalanceAndLiquidacionIVA_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"acct-ctrl-{Guid.NewGuid():N}"[..18]);

        var balance = await client.GetAsync("/api/accounting/balance?year=2026");
        var balanceBody = await balance.Content.ReadAsStringAsync();
        Assert.True(balance.StatusCode == HttpStatusCode.OK, $"trial balance → {balance.StatusCode}: {balanceBody}");

        var liquidacion = await client.GetAsync("/api/accounting/liquidacion-iva?year=2026&quarter=2");
        var liquidacionBody = await liquidacion.Content.ReadAsStringAsync();
        Assert.True(liquidacion.StatusCode == HttpStatusCode.OK, $"liquidacion → {liquidacion.StatusCode}: {liquidacionBody}");

        var mayor = await client.GetAsync("/api/accounting/mayor/572?year=2026");
        var mayorBody = await mayor.Content.ReadAsStringAsync();
        Assert.True(mayor.StatusCode == HttpStatusCode.OK, $"mayor → {mayor.StatusCode}: {mayorBody}");
    }

    [Fact]
    public async Task AccountingExport_LibroDiario_ReturnsCsv()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"acct-exp-{Guid.NewGuid():N}"[..18]);

        var response = await client.GetAsync("/api/accounting/export/libro-diario?year=2026");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("csv", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.OrdinalIgnoreCase);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 0);
    }

    [Fact]
    public async Task CrmFlow_CreateSupplier_Update_GetClientV1()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"crm-p12-{Guid.NewGuid():N}"[..18]);

        var createClient = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "Cliente P12",
            taxId = "87654321X",
            email = "p12@test.local",
            customFields = "{}",
        });
        Assert.Equal(HttpStatusCode.Created, createClient.StatusCode);
        var clientId = (await createClient.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var getClient = await client.GetAsync($"/api/v1/clients/{clientId}");
        Assert.Equal(HttpStatusCode.OK, getClient.StatusCode);

        var createSupplier = await client.PostAsJsonAsync("/api/suppliers", new
        {
            name = "Proveedor P12",
            taxId = "B12345674",
            email = "supp@test.local",
            phone = "600000000",
            address = "Calle 1",
        });
        Assert.Equal(HttpStatusCode.Created, createSupplier.StatusCode);
        var supplierId = (await createSupplier.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var updateSupplier = await client.PutAsJsonAsync($"/api/suppliers/{supplierId}", new
        {
            name = "Proveedor P12 actualizado",
            taxId = "B12345674",
            email = "supp-new@test.local",
            phone = "611111111",
            address = "Calle 2",
        });
        Assert.Equal(HttpStatusCode.OK, updateSupplier.StatusCode);
        var updated = await updateSupplier.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Proveedor P12 actualizado", updated.GetProperty("name").GetString());
    }

    [Fact]
    public async Task InventoryFlow_WarehouseCrud()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"inv-wh-{Guid.NewGuid():N}"[..18]);

        var create = await client.PostAsJsonAsync("/api/inventory/warehouses", new
        {
            name = "Almacén integración P12",
            location = "Valencia",
        });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var warehouseId = (await create.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var list = await client.GetAsync("/api/inventory/warehouses?active=true");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);

        var getById = await client.GetAsync($"/api/inventory/warehouses/{warehouseId}");
        Assert.Equal(HttpStatusCode.OK, getById.StatusCode);

        var update = await client.PutAsJsonAsync($"/api/inventory/warehouses/{warehouseId}", new
        {
            name = "Almacén P12 renombrado",
            location = "Sevilla",
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
    }

    [Fact]
    public async Task TreasuryFlow_PaymentOrders_CreateAndList()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"treasury-po-{Guid.NewGuid():N}"[..18]);

        var bankResponse = await client.PostAsJsonAsync("/api/treasury/bank-accounts", new
        {
            name = "Cuenta PO P12",
            iban = "ES7620770024003102575766",
            bic = "CAIXESBBXXX",
            bankName = "Caixa",
        });
        Assert.Equal(HttpStatusCode.Created, bankResponse.StatusCode);
        var bankId = (await bankResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var createOrder = await client.PostAsJsonAsync("/api/treasury/payment-orders", new
        {
            paymentType = "Supplier",
            beneficiaryName = "Proveedor orden P12",
            beneficiaryTaxId = "B12345674",
            beneficiaryIban = "ES9121000418450200051332",
            description = "Pago factura P12",
            amount = 450m,
            scheduledDate = DateTime.UtcNow.AddDays(14).ToString("O"),
            bankAccountId = bankId,
        });
        Assert.True(
            createOrder.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            $"POST payment-order → {createOrder.StatusCode}: {await createOrder.Content.ReadAsStringAsync()}");

        var list = await client.GetAsync("/api/treasury/payment-orders?pageSize=50");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var body = await list.Content.ReadAsStringAsync();
        Assert.Contains("Proveedor orden P12", body);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Phase12 Co {suffix}",
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
            Name = "Phase12 Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return (authedClient, body.CompanyId);
    }
}

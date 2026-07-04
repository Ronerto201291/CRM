using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 16: fix POST leads (migraciones CRM), ExecutePaymentOrder HTTP, AnulVerifactu sin Hangfire,
/// provisions/aging HTTP, convert lead.
/// </summary>
public class Phase16IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase16IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task CrmLeads_Post_Returns201()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"leads-p16-{Guid.NewGuid():N}"[..18]);

        var response = await client.PostAsJsonAsync("/api/leads", new
        {
            name = "Lead integración P16",
            email = "lead-p16@test.local",
            phone = "600123456",
            taxId = "B12345674",
            status = "New",
            source = "Web",
        });

        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Created,
            $"POST /api/leads → {response.StatusCode}: {body}");

        var lead = await response.Content.ReadFromJsonAsync<JsonElement>();
        var leadId = lead.GetProperty("id").GetGuid();
        Assert.Equal("Lead integración P16", lead.GetProperty("name").GetString());

        var getResponse = await client.GetAsync($"/api/leads/{leadId}");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
    }

    [Fact]
    public async Task CrmLeads_ConvertToClient_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"lead-conv-p16-{Guid.NewGuid():N}"[..18]);

        var createResponse = await client.PostAsJsonAsync("/api/leads", new
        {
            name = "Prospecto convertir P16",
            email = "convert-p16@test.local",
            taxId = "12345678Z",
            status = "Qualified",
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var leadId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var convertResponse = await client.PostAsync($"/api/leads/{leadId}/convert-to-client", null);
        var body = await convertResponse.Content.ReadAsStringAsync();
        Assert.True(convertResponse.StatusCode == HttpStatusCode.OK,
            $"convert → {convertResponse.StatusCode}: {body}");
        Assert.Contains("clientId", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Treasury_ExecutePaymentOrder_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"exec-po-p16-{Guid.NewGuid():N}"[..18]);

        var bankResponse = await client.PostAsJsonAsync("/api/treasury/bank-accounts", new
        {
            name = "Cuenta execute P16",
            iban = "ES9121000418450200051332",
            bic = "CAIXESBBXXX",
            bankName = "Caixa",
        });
        Assert.Equal(HttpStatusCode.Created, bankResponse.StatusCode);
        var bankId = (await bankResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var createOrder = await client.PostAsJsonAsync("/api/treasury/payment-orders", new
        {
            paymentType = "Supplier",
            beneficiaryName = "Proveedor execute P16",
            beneficiaryTaxId = "B12345674",
            beneficiaryIban = "ES7620770024003102575766",
            description = "Pago P16",
            amount = 320m,
            bankAccountId = bankId,
        });
        Assert.True(createOrder.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await createOrder.Content.ReadAsStringAsync());
        var orderId = (await createOrder.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var executeResponse = await client.PostAsync($"/api/treasury/payment-orders/{orderId}/execute", null);
        var body = await executeResponse.Content.ReadAsStringAsync();
        Assert.True(executeResponse.StatusCode == HttpStatusCode.OK,
            $"execute → {executeResponse.StatusCode}: {body}");
        Assert.Contains("Executed", body);
    }

    [Fact]
    public async Task Invoices_AnulVerifactu_Returns200_WhenHuellaPresent()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"anul-vf-p16-{Guid.NewGuid():N}"[..18]);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente Anul VF P16",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[] { new { description = "Anul VF", quantity = 1, unitPrice = 100, taxRate = 21, surchargeRate = 0, tipoOperacion = "Nacional" } },
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var invoiceId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var lockResponse = await client.PostAsync($"/api/invoices/{invoiceId}/lock", null);
        Assert.True(lockResponse.StatusCode == HttpStatusCode.OK,
            await lockResponse.Content.ReadAsStringAsync());

        var anulResponse = await client.PostAsync($"/api/invoices/{invoiceId}/verifactu/anular", null);
        var body = await anulResponse.Content.ReadAsStringAsync();
        Assert.True(anulResponse.StatusCode == HttpStatusCode.OK,
            $"anular → {anulResponse.StatusCode}: {body}");
    }

    [Fact]
    public async Task Accounting_Provisions_CreateAndList_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"prov-p16-{Guid.NewGuid():N}"[..18]);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/v1/accounting/provisions", new
        {
            code = "490",
            description = "Provisión insolvencias P16",
            amount = 1500m,
            dueDate = DateTime.UtcNow.AddMonths(6).ToString("O"),
        });
        Assert.True(createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await createResponse.Content.ReadAsStringAsync());

        var listResponse = await client.GetAsync("/api/v1/accounting/provisions?status=Active");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var body = await listResponse.Content.ReadAsStringAsync();
        Assert.Contains("Provisión insolvencias P16", body);
    }

    [Fact]
    public async Task Accounting_AgingReport_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"aging-p16-{Guid.NewGuid():N}"[..18]);

        var response = await client.GetAsync("/api/accounting/aging");
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"aging → {response.StatusCode}: {body}");
        Assert.Contains("receivables", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sales_Deliveries_List_Return200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"deliv-p16-{Guid.NewGuid():N}"[..18]);

        var response = await client.GetAsync("/api/v1/sales/deliveries");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task SeedPgcAsync(Guid companyId)
    {
        var connectionString = _factory.GetConnectionString();
        var tenant = new Erp.Infrastructure.Tenancy.TenantContext();
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var ctx = new AccountingDbContext(options, tenant);
        var seeder = new SeedChartOfAccountsHandler(ctx, NullLogger<SeedChartOfAccountsHandler>.Instance);
        await seeder.Handle(new CompanyCreatedEvent { CompanyId = companyId }, CancellationToken.None);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Phase16 Co {suffix}",
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
            Name = "Phase16 Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return (authedClient, body.CompanyId);
    }
}

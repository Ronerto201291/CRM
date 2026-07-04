using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Accounting.Infrastructure.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Bloquear factura → event handler contable → asiento en diario.
/// </summary>
public class LockInvoiceJournalEntryTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public LockInvoiceJournalEntryTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task LockDraftInvoice_CreatesAccountingJournalEntry()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"lock-je-{Guid.NewGuid():N}"[..18]);
        await SeedPgcAsync(companyId);

        var createResponse = await client.PostAsJsonAsync("/api/invoices", new
        {
            clientType = "Manual",
            clientName = "Cliente asiento",
            clientTaxId = "12345678Z",
            series = "A",
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            irpfRate = 0,
            invoiceType = "Normal",
            lines = new[]
            {
                new
                {
                    description = "Servicio contable",
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

        var lockResponse = await client.PostAsync($"/api/invoices/{invoiceId}/lock", null);
        Assert.True(
            lockResponse.StatusCode == HttpStatusCode.OK,
            $"POST lock → {lockResponse.StatusCode}: {await lockResponse.Content.ReadAsStringAsync()}");

        var journalResponse = await client.GetAsync("/api/accounting/journal?year=2026&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, journalResponse.StatusCode);

        var journalJson = await journalResponse.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(journalJson);
        var items = doc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : doc.RootElement;

        Assert.Equal(JsonValueKind.Array, items.ValueKind);
        Assert.True(items.GetArrayLength() >= 1);
        Assert.Contains(items.EnumerateArray(), el =>
            el.TryGetProperty("sourceType", out var st) && st.GetString() == "Invoice");
    }

    private async Task SeedPgcAsync(Guid companyId)
    {
        var connectionString = _factory.GetConnectionString();
        var tenant = new Erp.Infrastructure.Tenancy.TenantContext();
        var options = new DbContextOptionsBuilder<AccountingDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        await using var ctx = new AccountingDbContext(options, tenant);
        var seeder = new PgcSeeder(ctx, NullLogger<PgcSeeder>.Instance);
        await seeder.SeedAsync(companyId);
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Lock JE Co {suffix}",
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
        return (authedClient, registerBody.CompanyId);
    }
}

public class FiscalEndpointsSmokeTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public FiscalEndpointsSmokeTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetHomologationStatus_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync();
        var response = await client.GetAsync("/api/fiscal/homologation/status");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("verifactu", json, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ValidateSiiXml_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync();
        var response = await client.GetAsync("/api/sii/validate?year=2026&month=3&type=emitidas");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVerifactuXml_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync();
        var response = await client.GetAsync("/api/sii/verifactu?year=2026&month=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> RegisterAndAuthAsync()
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Fiscal Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"fiscal-{suffix}@test.local",
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

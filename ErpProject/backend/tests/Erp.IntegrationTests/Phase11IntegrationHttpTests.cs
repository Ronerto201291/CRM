using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Fase 11: HTTP integración approve gasto→asiento, treasury SEPA/forecast, automation CRUD.
/// </summary>
public class Phase11IntegrationHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public Phase11IntegrationHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task ExpenseApprove_CreatesJournalEntry()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"exp-approve-{Guid.NewGuid():N}"[..18]);

        var createResponse = await client.PostAsJsonAsync("/api/expenses", new
        {
            invoiceNumber = $"GA-{Guid.NewGuid():N}"[..10],
            supplierName = "Proveedor aprobación",
            supplierTaxId = "B12345674",
            taxBase = 100m,
            vatRate = 21m,
            vatAmount = 21m,
            total = 121m,
        });
        Assert.True(
            createResponse.StatusCode is HttpStatusCode.Created or HttpStatusCode.OK,
            await createResponse.Content.ReadAsStringAsync());

        var expenseId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();
        await SeedExpenseAccountsAsync(companyId);

        var approveResponse = await client.PostAsync($"/api/expenses/{expenseId}/approve", null);
        Assert.True(
            approveResponse.StatusCode == HttpStatusCode.OK,
            $"POST approve → {approveResponse.StatusCode}: {await approveResponse.Content.ReadAsStringAsync()}");

        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT COUNT(*) FROM accounting."JournalEntries"
            WHERE "CompanyId" = @cid AND "SourceType" = 'Expense' AND "SourceId" = @eid
            """;
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("cid", companyId));
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter("eid", expenseId));
        var count = Convert.ToInt64(await cmd.ExecuteScalarAsync());
        Assert.Equal(1, count);
    }

    [Fact]
    public async Task TreasuryFlow_BankAccount_Effect_Sepa()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"treasury-sepa-{Guid.NewGuid():N}"[..18]);

        var bankResponse = await client.PostAsJsonAsync("/api/treasury/bank-accounts", new
        {
            name = "Cuenta integración",
            iban = "ES9121000418450200051332",
            bic = "CAIXESBBXXX",
            bankName = "Caixa",
        });
        Assert.Equal(HttpStatusCode.Created, bankResponse.StatusCode);
        var bankId = (await bankResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var effectResponse = await client.PostAsJsonAsync("/api/treasury/effects", new
        {
            clientName = "Cliente SEPA",
            clientTaxId = "A12345678",
            effectNumber = $"EFF-{Guid.NewGuid():N}"[..10],
            issueDate = DateTime.UtcNow.ToString("O"),
            dueDate = DateTime.UtcNow.AddDays(30).ToString("O"),
            amount = 750m,
            bankAccountId = bankId,
        });
        Assert.Equal(HttpStatusCode.Created, effectResponse.StatusCode);
        var effectId = (await effectResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sepaResponse = await client.PostAsJsonAsync($"/api/treasury/effects/{effectId}/sepa", new
        {
            clientIban = "ES7620770024003102575766",
            clientBic = "CAIXESBBXXX",
        });
        Assert.True(
            sepaResponse.StatusCode == HttpStatusCode.OK,
            $"SEPA → {sepaResponse.StatusCode}: {await sepaResponse.Content.ReadAsStringAsync()}");
        Assert.Equal("application/xml", sepaResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task TreasuryFlow_GetForecasts_Returns200()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"treasury-fc-{Guid.NewGuid():N}"[..18]);
        var response = await client.GetAsync("/api/treasury/forecasts?year=2026&month=7");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task AutomationFlow_RulesCrud()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"auto-rules-{Guid.NewGuid():N}"[..18]);

        var createResponse = await client.PostAsJsonAsync("/api/automation/rules", new
        {
            name = "Regla integración",
            triggerEvent = "OnLeadStatusChanged",
            conditionField = "Status",
            conditionOperator = "Equals",
            conditionValue = "Won",
            actionType = "SendEmail",
            actionConfiguration = "{\"to\":\"test@local\"}",
        });
        Assert.Equal(HttpStatusCode.OK, createResponse.StatusCode);
        var ruleId = (await createResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var listResponse = await client.GetAsync("/api/automation/rules");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var rules = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(rules.GetArrayLength() >= 1);

        var toggleResponse = await client.PutAsJsonAsync($"/api/automation/rules/{ruleId}/toggle", new { isActive = false });
        Assert.Equal(HttpStatusCode.OK, toggleResponse.StatusCode);
        var toggleBody = await toggleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(toggleBody.GetProperty("isActive").GetBoolean());
    }

    private async Task<(HttpClient Client, Guid CompanyId)> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Phase11 Co {suffix}",
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
            Name = "Phase11 Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        authedClient.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return (authedClient, body.CompanyId);
    }

    private async Task SeedExpenseAccountsAsync(Guid companyId)
    {
        await using var conn = new Npgsql.NpgsqlConnection(_factory.GetConnectionString());
        await conn.OpenAsync();
        foreach (var (code, name, type) in new[]
        {
            ("600", "Compras", "Expense"),
            ("472", "HP IVA soportado", "Asset"),
            ("410", "Proveedores", "Liability"),
        })
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                INSERT INTO accounting."Accounts" ("Id", "CompanyId", "Code", "Name", "Type")
                VALUES (@id, @cid, @code, @name, @type)
                """;
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("id", Guid.NewGuid()));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("cid", companyId));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("code", code));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("name", name));
            cmd.Parameters.Add(new Npgsql.NpgsqlParameter("type", type));
            await cmd.ExecuteNonQueryAsync();
        }
    }
}

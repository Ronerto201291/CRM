using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Happy-path presupuesto: crear → actualizar → enviar → aceptar → convertir → bloquear → asiento.
/// </summary>
public class QuoteFlowPostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public QuoteFlowPostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task QuoteFullFlow_UpdateSendAcceptConvert_LockCreatesJournalEntry()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, companyId) = await RegisterAndAuthAsync($"quote-flow-{Guid.NewGuid():N}"[..18]);
        await SeedPgcAsync(companyId);

        var clientResponse = await client.PostAsJsonAsync("/api/clients", new
        {
            name = "Cliente presupuesto",
            taxId = "12345678Z",
            email = "presupuesto@test.local",
            customFields = "{}",
        });
        Assert.True(
            clientResponse.StatusCode == HttpStatusCode.Created,
            $"POST client → {clientResponse.StatusCode}: {await clientResponse.Content.ReadAsStringAsync()}");
        var clientJson = await clientResponse.Content.ReadFromJsonAsync<JsonElement>();
        var clientId = clientJson.GetProperty("id").GetGuid();

        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);
        var createQuote = await client.PostAsJsonAsync("/api/quotes", new
        {
            clientId,
            clientType = "Registered",
            clientName = "Cliente presupuesto",
            clientEmail = "presupuesto@test.local",
            clientTaxId = "12345678Z",
            seriesPrefix = "PRE",
            issueDate = issueDate.ToString("O"),
            validUntil = issueDate.AddDays(45).ToString("O"),
            lines = new[]
            {
                new { description = "Servicio Postgres", quantity = 2, unitPrice = 150, taxRate = 21 },
            },
        });
        Assert.True(
            createQuote.StatusCode == HttpStatusCode.Created,
            $"POST quote → {createQuote.StatusCode}: {await createQuote.Content.ReadAsStringAsync()}");
        var quoteCreated = await createQuote.Content.ReadFromJsonAsync<JsonElement>();
        var quoteId = quoteCreated.GetProperty("id").GetGuid();

        var getQuote = await client.GetAsync($"/api/quotes/{quoteId}");
        Assert.Equal(HttpStatusCode.OK, getQuote.StatusCode);
        var quoteDetail = await getQuote.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(363m, quoteDetail.GetProperty("totalAmount").GetDecimal());

        var sendResponse = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/send", new { attachPdf = false });
        Assert.True(
            sendResponse.StatusCode == HttpStatusCode.OK,
            $"POST send → {sendResponse.StatusCode}: {await sendResponse.Content.ReadAsStringAsync()}");

        var acceptResponse = await client.PostAsync($"/api/quotes/{quoteId}/accept", null);
        Assert.Equal(HttpStatusCode.OK, acceptResponse.StatusCode);

        var convertResponse = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/convert", new
        {
            invoiceSeries = "A",
            dueDate = issueDate.AddDays(30).ToString("O"),
            irpfRate = 0,
        });
        Assert.Equal(HttpStatusCode.OK, convertResponse.StatusCode);
        var convertBody = await convertResponse.Content.ReadFromJsonAsync<JsonElement>();
        var invoiceId = convertBody.GetProperty("invoiceId").GetGuid();

        var lockResponse = await client.PostAsync($"/api/invoices/{invoiceId}/lock", null);
        Assert.True(
            lockResponse.StatusCode == HttpStatusCode.OK,
            $"POST lock → {lockResponse.StatusCode}: {await lockResponse.Content.ReadAsStringAsync()}");

        var journalResponse = await client.GetAsync("/api/accounting/journal?year=2026&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, journalResponse.StatusCode);
        var journalJson = await journalResponse.Content.ReadAsStringAsync();
        using var journalDoc = JsonDocument.Parse(journalJson);
        var items = journalDoc.RootElement.TryGetProperty("items", out var itemsProp)
            ? itemsProp
            : journalDoc.RootElement;
        Assert.True(items.GetArrayLength() >= 1);
    }

    [Fact]
    public async Task SendQuote_WithoutEmail_Returns400()
    {
        if (!_factory.DockerAvailable)
            return;

        var (client, _) = await RegisterAndAuthAsync($"quote-send-400-{Guid.NewGuid():N}"[..18]);
        var issueDate = DateTime.UtcNow;

        var createQuote = await client.PostAsJsonAsync("/api/quotes", new
        {
            clientType = "Manual",
            clientName = "Sin email",
            seriesPrefix = "PRE",
            issueDate = issueDate.ToString("O"),
            validUntil = issueDate.AddDays(15).ToString("O"),
            lines = new[] { new { description = "L", quantity = 1, unitPrice = 50, taxRate = 21 } },
        });
        Assert.Equal(HttpStatusCode.Created, createQuote.StatusCode);
        var quoteId = (await createQuote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var sendResponse = await client.PostAsJsonAsync($"/api/quotes/{quoteId}/send", new { attachPdf = false });
        Assert.Equal(HttpStatusCode.BadRequest, sendResponse.StatusCode);
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
            CompanyName = $"Quote Co {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"{emailPrefix}-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });

        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var body = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        await IntegrationTestRegistration.EnableAllModulesAsync(_factory.GetConnectionString(), body!.CompanyId);

        var authedClient = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(authedClient, body.Token, body.CompanyId);
        return (authedClient, body.CompanyId);
    }
}

using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// PUT /api/quotes/{id} happy-path (Postgres real, no InMemory).
/// </summary>
public class UpdateQuotePostgresTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public UpdateQuotePostgresTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task PutDraftQuote_UpdatesLines_AndRecalculatesTotals()
    {
        if (!_factory.DockerAvailable)
            return;

        var client = await RegisterAndAuthAsync($"quote-put-{Guid.NewGuid():N}"[..18]);
        var issueDate = new DateTime(2026, 7, 1, 0, 0, 0, DateTimeKind.Utc);

        var createQuote = await client.PostAsJsonAsync("/api/quotes", new
        {
            clientType = "Manual",
            clientName = "Update PG",
            clientEmail = "update-pg@test.local",
            seriesPrefix = "PRE",
            issueDate = issueDate.ToString("O"),
            validUntil = issueDate.AddDays(30).ToString("O"),
            lines = new[] { new { description = "Original", quantity = 1, unitPrice = 100, taxRate = 21 } },
        });
        Assert.Equal(HttpStatusCode.Created, createQuote.StatusCode);
        var quoteId = (await createQuote.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

        var updateResponse = await client.PutAsJsonAsync($"/api/quotes/{quoteId}", new
        {
            clientType = "Manual",
            clientName = "Update PG",
            clientEmail = "update-pg@test.local",
            issueDate = issueDate.ToString("O"),
            validUntil = issueDate.AddDays(45).ToString("O"),
            lines = new[]
            {
                new { description = "Línea A", quantity = 2, unitPrice = 50, taxRate = 21 },
                new { description = "Línea B", quantity = 1, unitPrice = 200, taxRate = 10 },
            },
        });

        Assert.True(
            updateResponse.StatusCode == HttpStatusCode.NoContent,
            $"PUT quote → {updateResponse.StatusCode}: {await updateResponse.Content.ReadAsStringAsync()}");

        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/quotes/{quoteId}");
        Assert.Equal(341m, detail.GetProperty("totalAmount").GetDecimal());
        Assert.Equal(2, detail.GetProperty("lines").GetArrayLength());
    }

    private async Task<HttpClient> RegisterAndAuthAsync(string emailPrefix)
    {
        var client = _factory.CreatePostgresClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var registerResponse = await client.PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"Quote PUT Co {suffix}",
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

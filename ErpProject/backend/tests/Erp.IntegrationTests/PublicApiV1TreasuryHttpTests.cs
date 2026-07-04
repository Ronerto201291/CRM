using System.Net;
using System.Net.Http.Json;
using Erp.Application.Features.Auth.Commands;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Smoke HTTP v1 treasury con JWT + X-Api-Key (financing, currencies, consolidation).
/// </summary>
public class PublicApiV1TreasuryHttpTests : IClassFixture<PostgresWebApplicationFactory>
{
    private readonly PostgresWebApplicationFactory _factory;

    public PublicApiV1TreasuryHttpTests(PostgresWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task GetCurrencies_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = await CreateAuthedClientWithApiKeyAsync();
        var response = await client.GetAsync("/api/v1/treasury/currencies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfirming_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = await CreateAuthedClientWithApiKeyAsync();
        var response = await client.GetAsync("/api/v1/treasury/financing/confirming");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCreditLines_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = await CreateAuthedClientWithApiKeyAsync();
        var response = await client.GetAsync("/api/v1/treasury/financing/credit-lines");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidationGroups_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = await CreateAuthedClientWithApiKeyAsync();
        var response = await client.GetAsync("/api/v1/treasury/consolidation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<HttpClient> CreateAuthedClientWithApiKeyAsync()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var registerResponse = await _factory.CreatePostgresClient().PostAsJsonAsync("/api/auth/register", new RegisterCompanyCommand
        {
            CompanyName = $"V1 Treasury {suffix}",
            CompanyTaxId = IntegrationTestRegistration.NextTaxId(),
            CompanyAddress = "Calle Test 1",
            AdminEmail = $"v1-treasury-{suffix}@test.local",
            AdminPassword = "SecurePass1!",
            AdminFirstName = "Admin",
            AdminLastName = "Test",
        });
        Assert.Equal(HttpStatusCode.OK, registerResponse.StatusCode);
        var registerBody = await registerResponse.Content.ReadFromJsonAsync<RegisterCompanyResponse>();
        Assert.NotNull(registerBody);

        var client = _factory.CreatePostgresClient();
        TestAuthHelper.ApplyAuth(client, registerBody!.Token, registerBody.CompanyId);

        var keyResponse = await client.PostAsJsonAsync("/api/apikeys", new Erp.Application.Features.ApiKeys.Commands.CreateApiKeyCommand
        {
            Name = "V1 Integration",
            RateLimit = 500,
        });
        Assert.Equal(HttpStatusCode.OK, keyResponse.StatusCode);
        var keyBody = await keyResponse.Content.ReadFromJsonAsync<Erp.Application.Features.ApiKeys.Commands.CreateApiKeyResult>();
        Assert.NotNull(keyBody);

        client.DefaultRequestHeaders.Remove("X-Api-Key");
        client.DefaultRequestHeaders.Add("X-Api-Key", keyBody!.RawKey);
        return client;
    }
}

using System.Net;
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

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"v1-treasury-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;
        var response = await client.GetAsync("/api/v1/treasury/currencies");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConfirming_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"v1-treasury-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;
        var response = await client.GetAsync("/api/v1/treasury/financing/confirming");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetCreditLines_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"v1-treasury-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;
        var response = await client.GetAsync("/api/v1/treasury/financing/credit-lines");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetConsolidationGroups_WithJwtAndApiKey_Returns200()
    {
        if (!_factory.DockerAvailable) return;

        var client = (await IntegrationTestAuth.RegisterEnterpriseAsync(
            _factory, $"v1-treasury-{Guid.NewGuid():N}"[..18], withApiKey: true)).Client;
        var response = await client.GetAsync("/api/v1/treasury/consolidation");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}

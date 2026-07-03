using System.Net;
using Xunit;

namespace Erp.IntegrationTests;

public class HealthEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthEndpointTests(ErpWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task HealthLive_Returns200_WithoutDependencyChecks()
    {
        var response = await _client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task HealthReady_Returns200_WithStubbedDependencies()
    {
        var response = await _client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Health_Returns200_WithJsonBody()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("status", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task HealthInfo_Returns200_WithServiceMetadata()
    {
        var response = await _client.GetAsync("/health/info");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("service", body, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("uptimeSeconds", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Metrics_Returns200_PrometheusFormat()
    {
        var response = await _client.GetAsync("/metrics");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("#", body);
    }

    [Fact]
    public async Task ProtectedEndpoint_Returns401_WithoutToken()
    {
        var response = await _client.GetAsync("/api/clients");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

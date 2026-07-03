using System.Net;
using Xunit;

namespace Erp.IntegrationTests;

public class PublicApiEndpointTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly HttpClient _client;

    public PublicApiEndpointTests(ErpWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task PublicApiHealth_Returns200_WithoutApiKey()
    {
        var response = await _client.GetAsync("/api/v1/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task PublicApiInvoices_Returns401_WithoutApiKey()
    {
        var response = await _client.GetAsync("/api/v1/invoices");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task PublicQuotesPortal_Returns400_ForInvalidToken_WithoutApiKey()
    {
        var response = await _client.GetAsync("/api/v1/public/quotes/short");

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound,
            $"Expected 400/404, got {response.StatusCode}");
    }
}

using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// Al menos un test HTTP por controller: 401 sin JWT en endpoints protegidos (ADR-0018 #32).
/// </summary>
public class ControllerUnauthorizedTests : IClassFixture<ErpWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ControllerUnauthorizedTests(ErpWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    public static IEnumerable<object[]> ProtectedEndpoints()
        => ControllerEndpointDiscovery.DiscoverOneEndpointPerController()
            .Where(p => p.ExpectUnauthorized)
            .Select(p => new object[] { p.ControllerName, p.HttpMethod, p.Path });

    public static IEnumerable<object[]> UnprotectedEndpoints()
        => ControllerEndpointDiscovery.DiscoverOneEndpointPerController()
            .Where(p => !p.ExpectUnauthorized)
            .Select(p => new object[] { p.ControllerName, p.HttpMethod, p.Path });

    [Fact]
    public void Discovery_FindsAtLeastFortyControllers()
    {
        var probes = ControllerEndpointDiscovery.DiscoverOneEndpointPerController();
        Assert.True(probes.Count >= 40, $"Se esperaban ≥40 controllers, encontrados {probes.Count}");
    }

    [Theory]
    [MemberData(nameof(ProtectedEndpoints))]
    public async Task ProtectedEndpoint_Returns401_WithoutToken(string controllerName, string httpMethod, string path)
    {
        var response = httpMethod == "POST"
            ? await _client.PostAsJsonAsync(path, new { })
            : await _client.GetAsync(path);

        Assert.True(
            response.StatusCode == HttpStatusCode.Unauthorized,
            $"{controllerName} {httpMethod} {path} → esperado 401, recibido {response.StatusCode}");
    }

    [Theory]
    [MemberData(nameof(UnprotectedEndpoints))]
    public async Task UnprotectedEndpoint_Responds_WithoutToken(string controllerName, string httpMethod, string path)
    {
        var response = httpMethod == "POST"
            ? await _client.PostAsJsonAsync(path, new { })
            : await _client.GetAsync(path);

        Assert.True(
            response.StatusCode != HttpStatusCode.NotFound,
            $"{controllerName} {httpMethod} {path} → no debe ser 404 (endpoint inexistente)");
    }
}

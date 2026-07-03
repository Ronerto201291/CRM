using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

namespace Erp.IntegrationTests;

/// <summary>
/// Host de integración — entorno IntegrationTests con Redis en memoria y health stubs.
/// Testcontainers Postgres/Redis: ver <see cref="TestcontainersSmokeTests"/>.
/// </summary>
public class ErpWebApplicationFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile("appsettings.IntegrationTests.json", optional: true);
        });
    }
}

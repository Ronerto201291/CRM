using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>
/// WebApplicationFactory con Postgres real (Testcontainers) para flujos autenticados.
/// Requiere Docker; los tests omiten si no está disponible.
/// </summary>
public sealed class PostgresWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private PostgreSqlContainer? _postgres;
    private string? _connectionString;

    public bool DockerAvailable { get; private set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTests");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddJsonFile("appsettings.IntegrationTests.json", optional: true);
            if (!string.IsNullOrEmpty(_connectionString))
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                });
            }
        });
    }

    public async Task InitializeAsync()
    {
        try
        {
            _postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("erp_auth_tests")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await _postgres.StartAsync();
            _connectionString = _postgres.GetConnectionString();
            Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", _connectionString);
            await IntegrationTestDatabaseMigrator.MigrateAllAsync(_connectionString);
            DockerAvailable = true;
        }
        catch (Exception ex) when (IsDockerUnavailable(ex) || IsMigrationUnavailable(ex))
        {
            DockerAvailable = false;
        }
    }

    public async Task DisposeAsync()
    {
        if (_postgres is not null)
            await _postgres.DisposeAsync();
        await base.DisposeAsync();
    }

    internal static bool IsDockerUnavailable(Exception ex) =>
        ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
        || ex.GetType().FullName?.Contains("Docker", StringComparison.OrdinalIgnoreCase) == true
        || (ex.InnerException is not null && IsDockerUnavailable(ex.InnerException));

    internal static bool IsMigrationUnavailable(Exception ex) =>
        ex.Message.Contains("PendingModelChanges", StringComparison.OrdinalIgnoreCase)
        || (ex.InnerException is not null && IsMigrationUnavailable(ex.InnerException));

    /// <summary>Crea HttpClient con connection string de Testcontainers aplicada.</summary>
    public HttpClient CreatePostgresClient()
    {
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("Postgres container not initialized.");

        return WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = _connectionString,
                });
            });
        }).CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    internal string GetConnectionString()
    {
        if (string.IsNullOrEmpty(_connectionString))
            throw new InvalidOperationException("Postgres container not initialized.");
        return _connectionString;
    }
}

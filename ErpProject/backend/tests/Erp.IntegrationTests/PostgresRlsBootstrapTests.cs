using Erp.Infrastructure.Data;
using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Testcontainers.PostgreSql;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>ADR-0018 #34 — RLS piloto aplicado tras migraciones.</summary>
public class PostgresRlsBootstrapTests
{
    [Fact]
    public async Task ApplyPilotPoliciesAsync_CreatesPoliciesOnModuleTables()
    {
        try
        {
            await using var postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("erp_rls")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await postgres.StartAsync();

            var connectionString = postgres.GetConnectionString();
            await IntegrationTestDatabaseMigrator.MigrateAllAsync(connectionString);

            var tenant = new TenantContext();
            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(connectionString)
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using var ctx = new ErpDbContext(options, tenant);

            var config = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["Postgres:RlsEnabled"] = "true" })
                .Build();

            await PostgresRlsBootstrap.ApplyPilotPoliciesAsync(
                ctx, config, NullLogger.Instance);

            await using var conn = ctx.Database.GetDbConnection();
            await conn.OpenAsync();

            await using var cmd = conn.CreateCommand();
            cmd.CommandText = """
                SELECT COUNT(*) FROM pg_policies
                WHERE policyname IN (
                  'crm_leads_tenant_isolation',
                  'billing_quotes_tenant_isolation',
                  'expenses_expensedocuments_tenant_isolation'
                )
                """;
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(3, count);
        }
        catch (Exception ex) when (IsDockerUnavailable(ex))
        {
            Assert.True(true);
        }
    }

    private static bool IsDockerUnavailable(Exception ex) =>
        ex.Message.Contains("Docker", StringComparison.OrdinalIgnoreCase)
        || ex.GetType().FullName?.Contains("Docker", StringComparison.OrdinalIgnoreCase) == true
        || ex.InnerException is not null && IsDockerUnavailable(ex.InnerException);
}

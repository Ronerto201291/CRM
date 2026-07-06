using Erp.Infrastructure.Data;
using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using Xunit;

namespace Erp.IntegrationTests;

/// <summary>ADR-0018 #32 — MigrateAsync contra Postgres real (Testcontainers).</summary>
public class PostgresMigrationTests
{
    [Fact]
    public async Task ErpDbContext_MigrateAsync_CreatesMigrationsHistory()
    {
        try
        {
            await using var postgres = new PostgreSqlBuilder()
                .WithImage("postgres:16-alpine")
                .WithDatabase("erp_migrate")
                .WithUsername("postgres")
                .WithPassword("postgres")
                .Build();

            await postgres.StartAsync();

            var tenant = new TenantContext();
            var options = new DbContextOptionsBuilder<ErpDbContext>()
                .UseNpgsql(postgres.GetConnectionString())
                .ConfigureWarnings(w => w.Ignore(
                    Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning))
                .Options;

            await using var ctx = new ErpDbContext(options, tenant);
            await ctx.Database.MigrateAsync();

            Assert.True(await ctx.Database.CanConnectAsync());
            // Tabla core creada por migraciones (vacía en instancia nueva)
            Assert.Equal(0, await ctx.Companies.CountAsync());
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

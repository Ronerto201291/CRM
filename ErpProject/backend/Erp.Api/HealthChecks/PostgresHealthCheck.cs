using Erp.Infrastructure.Data;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Erp.Api.HealthChecks;

/// <summary>
/// Verifies PostgreSQL connectivity via EF Core CanConnectAsync.
/// </summary>
public class PostgresHealthCheck : IHealthCheck
{
    private readonly IServiceScopeFactory _scopeFactory;

    public PostgresHealthCheck(IServiceScopeFactory scopeFactory)
        => _scopeFactory = scopeFactory;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            using var scope  = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
            var ok = await db.Database.CanConnectAsync(ct);
            return ok
                ? HealthCheckResult.Healthy("PostgreSQL is reachable.")
                : HealthCheckResult.Unhealthy("PostgreSQL CanConnectAsync returned false.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("PostgreSQL unreachable.", ex);
        }
    }
}

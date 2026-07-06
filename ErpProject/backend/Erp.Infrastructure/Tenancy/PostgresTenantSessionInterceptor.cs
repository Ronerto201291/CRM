using System.Data.Common;
using Erp.Application.Common.Interfaces;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Erp.Infrastructure.Tenancy;

/// <summary>
/// ADR-0018 #34 — establece app.current_tenant en cada conexión Npgsql cuando RLS está activo.
/// </summary>
public class PostgresTenantSessionInterceptor : DbConnectionInterceptor
{
    private readonly ITenantContext _tenantContext;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PostgresTenantSessionInterceptor> _logger;

    public PostgresTenantSessionInterceptor(
        ITenantContext tenantContext,
        IConfiguration configuration,
        ILogger<PostgresTenantSessionInterceptor> logger)
    {
        _tenantContext = tenantContext;
        _configuration = configuration;
        _logger = logger;
    }

    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        await SetTenantSessionAsync(connection, cancellationToken);
        await base.ConnectionOpenedAsync(connection, eventData, cancellationToken);
    }

    public override void ConnectionOpened(DbConnection connection, ConnectionEndEventData eventData)
    {
        SetTenantSession(connection);
        base.ConnectionOpened(connection, eventData);
    }

    private bool IsRlsEnabled =>
        _configuration.GetValue("Postgres:RlsEnabled", false);

    private async Task SetTenantSessionAsync(DbConnection connection, CancellationToken ct)
    {
        if (!IsRlsEnabled || _tenantContext.TenantId is not Guid tenantId)
            return;

        if (connection is not NpgsqlConnection npgsql)
            return;

        try
        {
            await using var cmd = npgsql.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.current_tenant', @tid, false)";
            cmd.Parameters.AddWithValue("tid", tenantId.ToString());
            await cmd.ExecuteNonQueryAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo establecer app.current_tenant para {TenantId}", tenantId);
        }
    }

    private void SetTenantSession(DbConnection connection)
    {
        if (!IsRlsEnabled || _tenantContext.TenantId is not Guid tenantId)
            return;

        if (connection is not NpgsqlConnection npgsql)
            return;

        try
        {
            using var cmd = npgsql.CreateCommand();
            cmd.CommandText = "SELECT set_config('app.current_tenant', @tid, false)";
            cmd.Parameters.AddWithValue("tid", tenantId.ToString());
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo establecer app.current_tenant para {TenantId}", tenantId);
        }
    }
}

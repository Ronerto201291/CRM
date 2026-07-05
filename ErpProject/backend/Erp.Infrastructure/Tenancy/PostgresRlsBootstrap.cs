using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Erp.Infrastructure.Tenancy;

/// <summary>ADR-0018 #34 — aplica políticas RLS piloto tras migraciones EF.</summary>
public static class PostgresRlsBootstrap
{
    /// <summary>
    /// Idempotente: habilita RLS en tablas core cuando existen (post-MigrateAsync).
    /// No viable en docker-entrypoint-initdb.d porque las tablas aún no existen.
    /// </summary>
    public static async Task ApplyPilotPoliciesAsync(
        ErpDbContext ctx,
        IConfiguration configuration,
        ILogger logger,
        CancellationToken ct = default)
    {
        if (!configuration.GetValue("Postgres:RlsEnabled", false))
        {
            logger.LogWarning(
                "Postgres:RlsEnabled=false — RLS pilot policies NOT applied. " +
                "Multi-tenant isolation relies only on EF global query filters (ADR-0018 #70).");
            return;
        }

        const string sql = """
            DO $rls$
            DECLARE
              t text;
              pol_name text;
              rec record;
            BEGIN
              IF to_regclass('public."Companies"') IS NOT NULL THEN
                EXECUTE 'ALTER TABLE "Companies" ENABLE ROW LEVEL SECURITY';
                IF NOT EXISTS (
                  SELECT 1 FROM pg_policies
                  WHERE schemaname = 'public' AND tablename = 'Companies'
                    AND policyname = 'companies_tenant_isolation'
                ) THEN
                  EXECUTE $policy$
                    CREATE POLICY companies_tenant_isolation ON "Companies"
                      USING ("Id" = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                  $policy$;
                END IF;
              END IF;

              FOREACH t IN ARRAY ARRAY[
                'Users', 'Roles', 'TenantModules', 'TenantInvitations',
                'FiscalEvents', 'Subscriptions', 'ApiKeys', 'AuditLogs', 'Rules'
              ]
              LOOP
                IF to_regclass(format('public.%I', t)) IS NOT NULL THEN
                  EXECUTE format('ALTER TABLE %I ENABLE ROW LEVEL SECURITY', t);
                  pol_name := lower(t) || '_tenant_isolation';
                  IF NOT EXISTS (
                    SELECT 1 FROM pg_policies
                    WHERE schemaname = 'public' AND tablename = t AND policyname = pol_name
                  ) THEN
                    EXECUTE format(
                      'CREATE POLICY %I ON %I USING ("CompanyId" = NULLIF(current_setting(''app.current_tenant'', true), '''')::uuid)',
                      pol_name, t);
                  END IF;
                END IF;
              END LOOP;

              -- Módulos con CompanyId (defensa en profundidad, ADR-0018 #34 ampliado)
              FOR rec IN SELECT * FROM (VALUES
                ('billing', 'Invoices'),
                ('billing', 'Quotes'),
                ('crm', 'Clients'),
                ('crm', 'Suppliers'),
                ('crm', 'Leads'),
                ('crm', 'Contacts'),
                ('expenses', 'ExpenseDocuments'),
                ('sales', 'SalesOrders'),
                ('purchasing', 'PurchaseOrders')
              ) AS m(schema_name, table_name)
              LOOP
                IF to_regclass(format('%I.%I', rec.schema_name, rec.table_name)) IS NOT NULL THEN
                  EXECUTE format(
                    'ALTER TABLE %I.%I ENABLE ROW LEVEL SECURITY',
                    rec.schema_name, rec.table_name);
                  pol_name := rec.schema_name || '_' || lower(rec.table_name) || '_tenant_isolation';
                  IF NOT EXISTS (
                    SELECT 1 FROM pg_policies
                    WHERE schemaname = rec.schema_name
                      AND tablename = rec.table_name
                      AND policyname = pol_name
                  ) THEN
                    EXECUTE format(
                      'CREATE POLICY %I ON %I.%I USING ("CompanyId" = NULLIF(current_setting(''app.current_tenant'', true), '''')::uuid)',
                      pol_name, rec.schema_name, rec.table_name);
                  END IF;
                END IF;
              END LOOP;
            END
            $rls$;
            """;

        await ctx.Database.ExecuteSqlRawAsync(sql, ct);
        logger.LogInformation(
            "RLS pilot: políticas aplicadas (Companies + 9 tablas core + 9 tablas módulo) — Postgres:RlsEnabled=true");
    }
}

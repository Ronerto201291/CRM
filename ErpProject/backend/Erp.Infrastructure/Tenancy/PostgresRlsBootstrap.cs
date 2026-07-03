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
            return;

        const string sql = """
            DO $rls$
            DECLARE
              t text;
              pol_name text;
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

              -- Módulos billing/crm (defensa en profundidad, ADR-0018 #34)
              IF to_regclass('billing."Invoices"') IS NOT NULL THEN
                EXECUTE 'ALTER TABLE billing."Invoices" ENABLE ROW LEVEL SECURITY';
                IF NOT EXISTS (
                  SELECT 1 FROM pg_policies
                  WHERE schemaname = 'billing' AND tablename = 'Invoices'
                    AND policyname = 'billing_invoices_tenant_isolation'
                ) THEN
                  EXECUTE $policy$
                    CREATE POLICY billing_invoices_tenant_isolation ON billing."Invoices"
                      USING ("CompanyId" = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                  $policy$;
                END IF;
              END IF;

              IF to_regclass('crm."Clients"') IS NOT NULL THEN
                EXECUTE 'ALTER TABLE crm."Clients" ENABLE ROW LEVEL SECURITY';
                IF NOT EXISTS (
                  SELECT 1 FROM pg_policies
                  WHERE schemaname = 'crm' AND tablename = 'Clients'
                    AND policyname = 'crm_clients_tenant_isolation'
                ) THEN
                  EXECUTE $policy$
                    CREATE POLICY crm_clients_tenant_isolation ON crm."Clients"
                      USING ("CompanyId" = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                  $policy$;
                END IF;
              END IF;

              IF to_regclass('crm."Suppliers"') IS NOT NULL THEN
                EXECUTE 'ALTER TABLE crm."Suppliers" ENABLE ROW LEVEL SECURITY';
                IF NOT EXISTS (
                  SELECT 1 FROM pg_policies
                  WHERE schemaname = 'crm' AND tablename = 'Suppliers'
                    AND policyname = 'crm_suppliers_tenant_isolation'
                ) THEN
                  EXECUTE $policy$
                    CREATE POLICY crm_suppliers_tenant_isolation ON crm."Suppliers"
                      USING ("CompanyId" = NULLIF(current_setting('app.current_tenant', true), '')::uuid)
                  $policy$;
                END IF;
              END IF;
            END
            $rls$;
            """;

        await ctx.Database.ExecuteSqlRawAsync(sql, ct);
        logger.LogInformation(
            "RLS pilot: políticas aplicadas (Companies + 9 tablas core + billing.Invoices + crm.Clients/Suppliers) — Postgres:RlsEnabled=true");
    }
}

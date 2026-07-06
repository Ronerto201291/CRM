using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260705130000_AddMaxCompaniesAndAccountantExport")]
public partial class AddMaxCompaniesAndAccountantExport : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Plans" ADD COLUMN IF NOT EXISTS "MaxCompanies" integer NOT NULL DEFAULT 1;
            UPDATE "Plans" SET "MaxCompanies" = 1 WHERE "Name" IN ('Free', 'Starter', 'Professional');
            UPDATE "Plans" SET "MaxCompanies" = 9999 WHERE "Name" = 'Enterprise';

            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "AccountantEmail" text NULL;
            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "AccountantExportFrequency" text NOT NULL DEFAULT 'disabled';
            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "AccountantExportLastRunAt" timestamp with time zone NULL;

            INSERT INTO "Plans" ("Id", "Name", "Description", "MonthlyPrice", "YearlyPrice", "MaxUsers", "MaxInvoicesPerMonth", "MaxCompanies", "IsActive", "SortOrder")
            SELECT gen_random_uuid(), 'Gestoría', 'Plan para gestorías: hasta 15 empresas bajo una cuenta', 79, 790, 25, 2000, 15, true, 5
            WHERE NOT EXISTS (SELECT 1 FROM "Plans" WHERE "Name" = 'Gestoría');

            INSERT INTO "PlanModules" ("Id", "PlanId", "ModuleName", "IsIncluded")
            SELECT gen_random_uuid(), p."Id", m."ModuleName", true
            FROM "Plans" p
            CROSS JOIN (
                SELECT 'Billing' AS "ModuleName" UNION ALL SELECT 'CRM' UNION ALL SELECT 'Expenses'
                UNION ALL SELECT 'Accounting' UNION ALL SELECT 'Inventory' UNION ALL SELECT 'Treasury'
                UNION ALL SELECT 'Payroll' UNION ALL SELECT 'Purchasing' UNION ALL SELECT 'Sales'
            ) m
            WHERE p."Name" = 'Gestoría'
              AND NOT EXISTS (
                SELECT 1 FROM "PlanModules" pm WHERE pm."PlanId" = p."Id" AND pm."ModuleName" = m."ModuleName");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("DELETE FROM \"PlanModules\" WHERE \"PlanId\" IN (SELECT \"Id\" FROM \"Plans\" WHERE \"Name\" = 'Gestoría');");
        migrationBuilder.Sql("DELETE FROM \"Plans\" WHERE \"Name\" = 'Gestoría';");
        migrationBuilder.DropColumn(name: "MaxCompanies", table: "Plans");
        migrationBuilder.DropColumn(name: "AccountantEmail", table: "Companies");
        migrationBuilder.DropColumn(name: "AccountantExportFrequency", table: "Companies");
        migrationBuilder.DropColumn(name: "AccountantExportLastRunAt", table: "Companies");
    }
}

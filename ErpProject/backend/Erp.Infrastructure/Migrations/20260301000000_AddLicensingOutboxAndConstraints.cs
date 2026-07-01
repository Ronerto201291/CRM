using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLicensingOutboxAndConstraints : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Plans table ───────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    MonthlyPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    YearlyPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxUsers = table.Column<int>(type: "integer", nullable: false),
                    MaxInvoicesPerMonth = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            // ─── PlanModules table ─────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "PlanModules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleName = table.Column<string>(type: "text", nullable: false),
                    IsIncluded = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlanModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlanModules_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlanModules_PlanId",
                table: "PlanModules",
                column: "PlanId");

            // ─── OutboxMessages table ──────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "OutboxMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EventType = table.Column<string>(type: "text", nullable: false),
                    Payload = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true),
                    RetryCount = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessages", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_Status",
                table: "OutboxMessages",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessages_CreatedAt",
                table: "OutboxMessages",
                column: "CreatedAt");

            // CK_Stock_Quantity_NonNegative moved to migration 3 (InventoryModuleSchema)
            // because Stocks table is created there.

            // ─── Companies: add QrUploadEnabled + PublicUploadToken if missing ─
            // (columns already in schema from InitialCreate — skip if exists)

            // ─── Trigger: BEFORE DELETE on JournalEntryLines ──────────────
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION prevent_journalline_delete()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'DELETE on JournalEntryLines is forbidden by Ley 11/2021 Antifraude';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_no_delete_journallines
                BEFORE DELETE ON public.""JournalEntryLines""
                FOR EACH ROW EXECUTE FUNCTION prevent_journalline_delete();
            ");

            // ─── Trigger: BEFORE DELETE on AuditLogs ─────────────────────
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION prevent_auditlog_delete()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'DELETE on AuditLogs is forbidden by Ley 11/2021 Antifraude';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_no_delete_auditlogs
                BEFORE DELETE ON public.""AuditLogs""
                FOR EACH ROW EXECUTE FUNCTION prevent_auditlog_delete();
            ");

            // ─── Seed default SaaS plans ──────────────────────────────────
            migrationBuilder.Sql(@"
                INSERT INTO ""Plans"" (""Id"", ""Name"", ""Description"", ""MonthlyPrice"", ""YearlyPrice"", ""MaxUsers"", ""MaxInvoicesPerMonth"", ""IsActive"", ""SortOrder"")
                VALUES
                  (gen_random_uuid(), 'Free',         'Plan gratuito: facturación básica',          0,     0,     1,   20,  true, 1),
                  (gen_random_uuid(), 'Starter',      'Para autónomos y pequeñas empresas',        19,   190,    3,  100,  true, 2),
                  (gen_random_uuid(), 'Professional', 'Para PYMES con contabilidad completa',      49,   490,   10,  500,  true, 3),
                  (gen_random_uuid(), 'Enterprise',   'Sin límites, API pública y OCR avanzado',  99,   990, 9999, 9999, true, 4);

                -- Modules per plan
                INSERT INTO ""PlanModules"" (""Id"", ""PlanId"", ""ModuleName"", ""IsIncluded"")
                SELECT gen_random_uuid(), p.""Id"", m.""ModuleName"", m.""IsIncluded""
                FROM ""Plans"" p
                CROSS JOIN (
                    SELECT 'Free' as plan, 'Billing' as ""ModuleName"", true as ""IsIncluded""
                    UNION ALL SELECT 'Free', 'CRM', true
                    UNION ALL SELECT 'Free', 'Expenses', false
                    UNION ALL SELECT 'Free', 'Accounting', false
                    UNION ALL SELECT 'Free', 'Inventory', false
                    UNION ALL SELECT 'Free', 'OCR', false
                    UNION ALL SELECT 'Free', 'PublicApi', false
                    UNION ALL SELECT 'Starter', 'Billing', true
                    UNION ALL SELECT 'Starter', 'CRM', true
                    UNION ALL SELECT 'Starter', 'Expenses', true
                    UNION ALL SELECT 'Starter', 'Accounting', true
                    UNION ALL SELECT 'Starter', 'Inventory', false
                    UNION ALL SELECT 'Starter', 'OCR', false
                    UNION ALL SELECT 'Starter', 'PublicApi', false
                    UNION ALL SELECT 'Professional', 'Billing', true
                    UNION ALL SELECT 'Professional', 'CRM', true
                    UNION ALL SELECT 'Professional', 'Expenses', true
                    UNION ALL SELECT 'Professional', 'Accounting', true
                    UNION ALL SELECT 'Professional', 'Inventory', true
                    UNION ALL SELECT 'Professional', 'OCR', true
                    UNION ALL SELECT 'Professional', 'PublicApi', false
                    UNION ALL SELECT 'Enterprise', 'Billing', true
                    UNION ALL SELECT 'Enterprise', 'CRM', true
                    UNION ALL SELECT 'Enterprise', 'Expenses', true
                    UNION ALL SELECT 'Enterprise', 'Accounting', true
                    UNION ALL SELECT 'Enterprise', 'Inventory', true
                    UNION ALL SELECT 'Enterprise', 'OCR', true
                    UNION ALL SELECT 'Enterprise', 'PublicApi', true
                ) m
                WHERE p.""Name"" = m.plan;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_no_delete_journallines ON public.""JournalEntryLines"";");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_no_delete_auditlogs ON public.""AuditLogs"";");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS prevent_journalline_delete();");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS prevent_auditlog_delete();");
            migrationBuilder.DropCheckConstraint(name: "CK_Stock_Quantity_NonNegative", table: "Stocks");
            migrationBuilder.DropTable(name: "PlanModules");
            migrationBuilder.DropTable(name: "Plans");
            migrationBuilder.DropTable(name: "OutboxMessages");
        }
    }
}

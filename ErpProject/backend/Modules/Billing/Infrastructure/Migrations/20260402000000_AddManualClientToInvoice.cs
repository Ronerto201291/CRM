using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddManualClientToInvoice : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── ClientId: Guid (NOT NULL) → Guid? (NULL) ──────────────────────────
            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "billing",
                table: "Invoices",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            // ── Nuevo: ClientType (distingue Registered / Manual) ──────────────────
            migrationBuilder.AddColumn<string>(
                name: "ClientType",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "Registered");

            // ── Nuevo: ClientEmail (snapshot fiscal para envío de facturas) ─────────
            migrationBuilder.AddColumn<string>(
                name: "ClientEmail",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: true);

            // ── Nuevo: campos snapshot ya definidos en el modelo pero ausentes en DB ─
            // (ClientNif, ClientName, ClientAddress, CompanyNif, CompanyName, CompanyAddress)
            // Sólo los añadimos si no existen. Si la migración anterior los creó, estos fallarán
            // y deberán eliminarse. Normalmente la InitialCreate ya los incluye según el modelo.
            // Por seguridad los añadimos con IF NOT EXISTS vía SQL raw.
            migrationBuilder.Sql(@"
                ALTER TABLE billing.""Invoices""
                    ADD COLUMN IF NOT EXISTS ""ClientNif""     text,
                    ADD COLUMN IF NOT EXISTS ""ClientName""    text,
                    ADD COLUMN IF NOT EXISTS ""ClientAddress"" text,
                    ADD COLUMN IF NOT EXISTS ""CompanyNif""    text,
                    ADD COLUMN IF NOT EXISTS ""CompanyName""   text,
                    ADD COLUMN IF NOT EXISTS ""CompanyAddress"" text,
                    ADD COLUMN IF NOT EXISTS ""OperationDate"" timestamp with time zone;
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientEmail",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClientType",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.AlterColumn<Guid>(
                name: "ClientId",
                schema: "billing",
                table: "Invoices",
                type: "uuid",
                nullable: false,
                defaultValue: Guid.Empty,
                oldClrType: typeof(Guid),
                oldNullable: true,
                oldType: "uuid");
        }
    }
}

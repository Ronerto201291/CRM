using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePublicViewToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicViewToken",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<bool>(
                name: "VerifactuRealtimeSubmission",
                schema: "billing",
                table: "Invoices",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifactuSubmittedAt",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VerifactuSubmissionLogs",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: false),
                    SubmissionType = table.Column<string>(type: "text", nullable: false),
                    EstadoEnvio = table.Column<string>(type: "text", nullable: false),
                    Success = table.Column<bool>(type: "boolean", nullable: false),
                    IsProduction = table.Column<bool>(type: "boolean", nullable: false),
                    RawResponse = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VerifactuSubmissionLogs", x => x.Id);
                });

            // Backfill: AddColumn above gives every existing invoice the same default
            // value (""), which would make the unique index below fail immediately on
            // any database with more than one invoice already in it. Assign each
            // existing row a distinct token before the index is created.
            migrationBuilder.Sql(@"
                UPDATE billing.""Invoices"" SET ""PublicViewToken"" = replace(gen_random_uuid()::text, '-', '');
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PublicViewToken",
                schema: "billing",
                table: "Invoices",
                column: "PublicViewToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VerifactuSubmissionLogs_CompanyId_InvoiceId",
                schema: "billing",
                table: "VerifactuSubmissionLogs",
                columns: new[] { "CompanyId", "InvoiceId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VerifactuSubmissionLogs",
                schema: "billing");

            migrationBuilder.DropIndex(
                name: "IX_Invoices_PublicViewToken",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PublicViewToken",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VerifactuRealtimeSubmission",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "VerifactuSubmittedAt",
                schema: "billing",
                table: "Invoices");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDocumentTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_FiscalEvents_CompanyId_DeadlineDate",
                table: "FiscalEvents");

            migrationBuilder.DropIndex(
                name: "IX_FiscalEvents_CompanyId_Year",
                table: "FiscalEvents");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "StripeWebhookEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<string>(
                name: "EventId",
                table: "StripeWebhookEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(255)",
                oldMaxLength: 255);

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FiscalEvents",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "FiscalEvents",
                type: "numeric",
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)",
                oldPrecision: 18,
                oldScale: 4,
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchingToleranceAmount",
                table: "Companies",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.CreateTable(
                name: "Documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    ObjectKey = table.Column<string>(type: "text", nullable: false),
                    EntityType = table.Column<string>(type: "text", nullable: true),
                    EntityId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UserCompanies_RoleId",
                table: "UserCompanies",
                column: "RoleId");

            migrationBuilder.CreateIndex(
                name: "IX_Documents_CompanyId_EntityType_EntityId",
                table: "Documents",
                columns: new[] { "CompanyId", "EntityType", "EntityId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Documents");

            migrationBuilder.DropIndex(
                name: "IX_UserCompanies_RoleId",
                table: "UserCompanies");

            migrationBuilder.AlterColumn<string>(
                name: "EventType",
                table: "StripeWebhookEvents",
                type: "character varying(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "EventId",
                table: "StripeWebhookEvents",
                type: "character varying(255)",
                maxLength: 255,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "FiscalEvents",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "Amount",
                table: "FiscalEvents",
                type: "numeric(18,4)",
                precision: 18,
                scale: 4,
                nullable: true,
                oldClrType: typeof(decimal),
                oldType: "numeric",
                oldNullable: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "MatchingToleranceAmount",
                table: "Companies",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.CreateIndex(
                name: "IX_FiscalEvents_CompanyId_DeadlineDate",
                table: "FiscalEvents",
                columns: new[] { "CompanyId", "DeadlineDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalEvents_CompanyId_Year",
                table: "FiscalEvents",
                columns: new[] { "CompanyId", "Year" });
        }
    }
}

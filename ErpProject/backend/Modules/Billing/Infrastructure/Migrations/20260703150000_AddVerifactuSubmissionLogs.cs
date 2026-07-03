using System;
using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260703150000_AddVerifactuSubmissionLogs")]
public partial class AddVerifactuSubmissionLogs : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "VerifactuSubmissionLogs",
            schema: "billing",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceNumber = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                SubmissionType = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                EstadoEnvio = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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

        migrationBuilder.CreateIndex(
            name: "IX_VerifactuSubmissionLogs_CompanyId_InvoiceId",
            schema: "billing",
            table: "VerifactuSubmissionLogs",
            columns: new[] { "CompanyId", "InvoiceId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "VerifactuSubmissionLogs", schema: "billing");
    }
}

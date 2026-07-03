using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

public partial class AddVerifactuRealtimeSubmissionToInvoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "VerifactuRealtimeSubmission",
            schema: "billing",
            table: "Invoices",
            type: "boolean",
            nullable: false,
            defaultValue: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VerifactuRealtimeSubmission",
            schema: "billing",
            table: "Invoices");
    }
}

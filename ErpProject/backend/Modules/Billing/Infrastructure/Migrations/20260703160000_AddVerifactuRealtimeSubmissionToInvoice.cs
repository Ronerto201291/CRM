using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260703160000_AddVerifactuRealtimeSubmissionToInvoice")]
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

using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260704000000_AddVerifactuSubmittedAtToInvoice")]
public partial class AddVerifactuSubmittedAtToInvoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "VerifactuSubmittedAt",
            schema: "billing",
            table: "Invoices",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VerifactuSubmittedAt",
            schema: "billing",
            table: "Invoices");
    }
}

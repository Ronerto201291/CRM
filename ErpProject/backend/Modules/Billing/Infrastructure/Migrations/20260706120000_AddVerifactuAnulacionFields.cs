using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260706120000_AddVerifactuAnulacionFields")]
public partial class AddVerifactuAnulacionFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "VerifactuAnulacionHuella",
            schema: "billing",
            table: "Invoices",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "VerifactuAnulacionAt",
            schema: "billing",
            table: "Invoices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "VerifactuAnulacionSubmittedAt",
            schema: "billing",
            table: "Invoices",
            type: "timestamp with time zone",
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "VerifactuAnulacionHuella", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "VerifactuAnulacionAt", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "VerifactuAnulacionSubmittedAt", schema: "billing", table: "Invoices");
    }
}

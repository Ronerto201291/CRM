using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

public partial class AddBillingInvoiceNumberToCustomerInvoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "BillingInvoiceNumber",
            schema: "sales",
            table: "CustomerInvoices",
            type: "character varying(50)",
            maxLength: 50,
            nullable: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "BillingInvoiceNumber",
            schema: "sales",
            table: "CustomerInvoices");
    }
}

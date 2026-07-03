using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

public partial class AddBillingInvoiceIdToCustomerInvoice : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "BillingInvoiceId",
            schema: "sales",
            table: "CustomerInvoices",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_CustomerInvoices_BillingInvoiceId",
            schema: "sales",
            table: "CustomerInvoices",
            column: "BillingInvoiceId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CustomerInvoices_BillingInvoiceId",
            schema: "sales",
            table: "CustomerInvoices");

        migrationBuilder.DropColumn(
            name: "BillingInvoiceId",
            schema: "sales",
            table: "CustomerInvoices");
    }
}

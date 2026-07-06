using Erp.Modules.Sales.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

[DbContext(typeof(SalesDbContext))]
[Migration("20260703120000_AddBillingInvoiceIdToCustomerInvoice")]
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

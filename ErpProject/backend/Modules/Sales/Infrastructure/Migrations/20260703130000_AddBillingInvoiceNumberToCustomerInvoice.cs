using Erp.Modules.Sales.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

[DbContext(typeof(SalesDbContext))]
[Migration("20260703130000_AddBillingInvoiceNumberToCustomerInvoice")]
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

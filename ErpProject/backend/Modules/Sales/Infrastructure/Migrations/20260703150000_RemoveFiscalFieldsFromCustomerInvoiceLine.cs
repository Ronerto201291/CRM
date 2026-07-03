using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

public partial class RemoveFiscalFieldsFromCustomerInvoiceLine : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "TaxRate", schema: "sales", table: "CustomerInvoiceLines");
        migrationBuilder.DropColumn(name: "TaxAmount", schema: "sales", table: "CustomerInvoiceLines");
        migrationBuilder.DropColumn(name: "TipoOperacion", schema: "sales", table: "CustomerInvoiceLines");
        migrationBuilder.DropColumn(name: "SurchargeRate", schema: "sales", table: "CustomerInvoiceLines");
        migrationBuilder.DropColumn(name: "SurchargeAmount", schema: "sales", table: "CustomerInvoiceLines");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(name: "TaxRate", schema: "sales", table: "CustomerInvoiceLines", type: "numeric", nullable: false, defaultValue: 21m);
        migrationBuilder.AddColumn<decimal>(name: "TaxAmount", schema: "sales", table: "CustomerInvoiceLines", type: "numeric", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<string>(name: "TipoOperacion", schema: "sales", table: "CustomerInvoiceLines", type: "text", nullable: false, defaultValue: "Nacional");
        migrationBuilder.AddColumn<decimal>(name: "SurchargeRate", schema: "sales", table: "CustomerInvoiceLines", type: "numeric", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<decimal>(name: "SurchargeAmount", schema: "sales", table: "CustomerInvoiceLines", type: "numeric", nullable: false, defaultValue: 0m);
    }
}

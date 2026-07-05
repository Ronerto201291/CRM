using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations;

[DbContext(typeof(BillingDbContext))]
[Migration("20260705130000_AddInvoiceMultiCurrency")]
public partial class AddInvoiceMultiCurrency : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "CurrencyCode",
            schema: "billing",
            table: "Invoices",
            type: "character(3)",
            maxLength: 3,
            nullable: false,
            defaultValue: "EUR");

        migrationBuilder.AddColumn<decimal>(
            name: "ExchangeRateToEur",
            schema: "billing",
            table: "Invoices",
            type: "numeric(18,8)",
            precision: 18,
            scale: 8,
            nullable: false,
            defaultValue: 1m);

        migrationBuilder.AddColumn<decimal>(
            name: "TotalEur",
            schema: "billing",
            table: "Invoices",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.Sql("""
            UPDATE billing."Invoices" SET "TotalEur" = "Total" WHERE "TotalEur" = 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "CurrencyCode", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "ExchangeRateToEur", schema: "billing", table: "Invoices");
        migrationBuilder.DropColumn(name: "TotalEur", schema: "billing", table: "Invoices");
    }
}

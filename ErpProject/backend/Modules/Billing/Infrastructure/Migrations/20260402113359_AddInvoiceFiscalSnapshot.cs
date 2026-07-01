using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceFiscalSnapshot : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxBaseAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubtotalBeforeDisc",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubtotalAfterDisc",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GlobalDiscountPct",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "GlobalDiscountAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "billing",
                table: "Quotes",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character(3)");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,4)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotalAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTaxBase",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTaxAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineSubtotal",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPct",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(5,2)");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric(18,2)");

            migrationBuilder.AlterColumn<string>(
                name: "ClientType",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Registered");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<decimal>(
                name: "TotalAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxBaseAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubtotalBeforeDisc",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "SubtotalAfterDisc",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "GlobalDiscountPct",
                schema: "billing",
                table: "Quotes",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "GlobalDiscountAmount",
                schema: "billing",
                table: "Quotes",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "Currency",
                schema: "billing",
                table: "Quotes",
                type: "character(3)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<decimal>(
                name: "TaxRate",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTotalAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTaxBase",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineTaxAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "LineSubtotal",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountPct",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(5,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<decimal>(
                name: "DiscountAmount",
                schema: "billing",
                table: "QuoteLines",
                type: "numeric(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "numeric");

            migrationBuilder.AlterColumn<string>(
                name: "ClientType",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "Registered",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

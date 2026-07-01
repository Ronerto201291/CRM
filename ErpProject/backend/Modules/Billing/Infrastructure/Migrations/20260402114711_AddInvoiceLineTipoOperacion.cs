using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceLineTipoOperacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TipoOperacion",
                schema: "billing",
                table: "InvoiceLines",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Nacional");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "TipoOperacion",
                schema: "billing",
                table: "InvoiceLines",
                type: "text",
                nullable: false,
                defaultValue: "Nacional",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

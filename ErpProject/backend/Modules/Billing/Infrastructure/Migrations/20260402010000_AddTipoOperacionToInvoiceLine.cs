using Erp.Modules.Billing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(BillingDbContext))]
    [Migration("20260402010000_AddTipoOperacionToInvoiceLine")]
    public partial class AddTipoOperacionToInvoiceLine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // TipoOperacion: "Nacional" | "IntraComunitario" | "Exportacion"
            // Necesario para clasificar líneas correctamente en Modelo 303 (casillas 59/60).
            migrationBuilder.AddColumn<string>(
                name: "TipoOperacion",
                schema: "billing",
                table: "InvoiceLines",
                type: "text",
                nullable: false,
                defaultValue: "Nacional");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TipoOperacion",
                schema: "billing",
                table: "InvoiceLines");
        }
    }
}

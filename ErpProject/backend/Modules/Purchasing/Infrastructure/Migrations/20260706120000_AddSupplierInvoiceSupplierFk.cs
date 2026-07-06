using Erp.Modules.Purchasing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Purchasing.Infrastructure.Migrations;

/// <summary>
/// FK purchasing.SupplierInvoices.SupplierId → crm.Suppliers.Id (trazabilidad proveedor CRM).
/// </summary>
[DbContext(typeof(PurchasingDbContext))]
[Migration("20260706120000_AddSupplierInvoiceSupplierFk")]
public partial class AddSupplierInvoiceSupplierFk : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE purchasing."SupplierInvoices"
            ADD COLUMN IF NOT EXISTS "SupplierId" uuid NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_SupplierInvoices_SupplierId",
            schema: "purchasing",
            table: "SupplierInvoices",
            column: "SupplierId");

        migrationBuilder.Sql("""
            ALTER TABLE purchasing."SupplierInvoices"
            ADD CONSTRAINT "FK_SupplierInvoices_Suppliers_SupplierId"
            FOREIGN KEY ("SupplierId") REFERENCES crm."Suppliers"("Id")
            ON DELETE RESTRICT;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE purchasing."SupplierInvoices"
            DROP CONSTRAINT IF EXISTS "FK_SupplierInvoices_Suppliers_SupplierId";
            """);

        migrationBuilder.DropIndex(
            name: "IX_SupplierInvoices_SupplierId",
            schema: "purchasing",
            table: "SupplierInvoices");

        migrationBuilder.DropColumn(
            name: "SupplierId",
            schema: "purchasing",
            table: "SupplierInvoices");
    }
}

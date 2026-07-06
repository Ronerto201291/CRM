using Erp.Modules.Purchasing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Purchasing.Infrastructure.Migrations;

/// <summary>
/// FK purchasing.PurchaseOrders.SupplierId → crm.Suppliers.Id (trazabilidad proveedor CRM).
/// </summary>
[DbContext(typeof(PurchasingDbContext))]
[Migration("20260705120000_AddPurchaseOrderSupplierFk")]
public partial class AddPurchaseOrderSupplierFk : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE purchasing."PurchaseOrders"
            ADD COLUMN IF NOT EXISTS "SupplierId" uuid NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_PurchaseOrders_SupplierId",
            schema: "purchasing",
            table: "PurchaseOrders",
            column: "SupplierId");

        migrationBuilder.Sql("""
            ALTER TABLE purchasing."PurchaseOrders"
            ADD CONSTRAINT "FK_PurchaseOrders_Suppliers_SupplierId"
            FOREIGN KEY ("SupplierId") REFERENCES crm."Suppliers"("Id")
            ON DELETE RESTRICT;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE purchasing."PurchaseOrders"
            DROP CONSTRAINT IF EXISTS "FK_PurchaseOrders_Suppliers_SupplierId";
            """);

        migrationBuilder.DropIndex(
            name: "IX_PurchaseOrders_SupplierId",
            schema: "purchasing",
            table: "PurchaseOrders");

        migrationBuilder.DropColumn(
            name: "SupplierId",
            schema: "purchasing",
            table: "PurchaseOrders");
    }
}

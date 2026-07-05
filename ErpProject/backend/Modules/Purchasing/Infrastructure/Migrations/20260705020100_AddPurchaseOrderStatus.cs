using Erp.Modules.Purchasing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Purchasing.Infrastructure.Migrations;

[DbContext(typeof(PurchasingDbContext))]
[Migration("20260705020100_AddPurchaseOrderStatus")]
public partial class AddPurchaseOrderStatus : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE purchasing."PurchaseOrders"
            ADD COLUMN IF NOT EXISTS "Status" text NOT NULL DEFAULT 'Approved';
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Status",
            schema: "purchasing",
            table: "PurchaseOrders");
    }
}

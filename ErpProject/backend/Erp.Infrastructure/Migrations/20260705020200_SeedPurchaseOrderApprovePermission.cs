using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260705020200_SeedPurchaseOrderApprovePermission")]
public partial class SeedPurchaseOrderApprovePermission : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            INSERT INTO "Permissions" ("Id", "Code", "Description", "Resource", "Action")
            SELECT gen_random_uuid(), 'PurchaseOrder:Approve', 'Approve PurchaseOrder', 'PurchaseOrder', 'Approve'
            WHERE NOT EXISTS (
                SELECT 1 FROM "Permissions" WHERE "Code" = 'PurchaseOrder:Approve'
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DELETE FROM "Permissions" WHERE "Code" = 'PurchaseOrder:Approve';
            """);
    }
}

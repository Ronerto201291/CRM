using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Sales.Infrastructure.Migrations;

/// <summary>
/// FK sales.SalesOrders.ClientId → crm.Clients.Id (ítem ADR-0018 #23).
/// </summary>
public partial class AddSalesOrderClientFk : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_SalesOrders_ClientId",
            schema: "sales",
            table: "SalesOrders",
            column: "ClientId");

        migrationBuilder.Sql("""
            ALTER TABLE sales."SalesOrders"
            ADD CONSTRAINT "FK_SalesOrders_Clients_ClientId"
            FOREIGN KEY ("ClientId") REFERENCES crm."Clients"("Id")
            ON DELETE RESTRICT;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE sales."SalesOrders"
            DROP CONSTRAINT IF EXISTS "FK_SalesOrders_Clients_ClientId";
            """);

        migrationBuilder.DropIndex(
            name: "IX_SalesOrders_ClientId",
            schema: "sales",
            table: "SalesOrders");
    }
}

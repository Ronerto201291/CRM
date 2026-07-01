using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddInventoryReorderPoint : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<decimal>(
            name: "ReorderPoint",
            table: "InventoryProducts",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "ReorderQty",
            table: "InventoryProducts",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "ReorderPoint",  table: "InventoryProducts");
        migrationBuilder.DropColumn(name: "ReorderQty",    table: "InventoryProducts");
    }
}

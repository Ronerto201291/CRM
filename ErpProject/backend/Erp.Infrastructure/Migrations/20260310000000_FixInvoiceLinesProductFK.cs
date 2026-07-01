using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>
/// CRITICAL: must run BEFORE 20260321000000_InventoryModuleSchema.
/// That migration drops the legacy Products table, but InvoiceLines holds a FK to it.
/// PostgreSQL will refuse to drop Products while a FK constraint exists.
/// This migration removes that FK and makes ProductId nullable, matching the entity model
/// (InvoiceLine.ProductId is Guid? — the link is optional / supply-only).
/// </summary>
public partial class FixInvoiceLinesProductFK : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Drop FK constraint InvoiceLines → Products
        migrationBuilder.DropForeignKey(
            name: "FK_InvoiceLines_Products_ProductId",
            table: "InvoiceLines");

        // Drop the index created for that FK
        migrationBuilder.DropIndex(
            name: "IX_InvoiceLines_ProductId",
            table: "InvoiceLines");

        // Make ProductId nullable to match entity (Guid?)
        migrationBuilder.AlterColumn<Guid>(
            name: "ProductId",
            table: "InvoiceLines",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: false);

        // Recreate index (without FK, just for query performance)
        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_ProductId",
            table: "InvoiceLines",
            column: "ProductId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_InvoiceLines_ProductId",
            table: "InvoiceLines");

        migrationBuilder.AlterColumn<Guid>(
            name: "ProductId",
            table: "InvoiceLines",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_InvoiceLines_ProductId",
            table: "InvoiceLines",
            column: "ProductId");

        migrationBuilder.AddForeignKey(
            name: "FK_InvoiceLines_Products_ProductId",
            table: "InvoiceLines",
            column: "ProductId",
            principalTable: "Products",
            principalColumn: "Id",
            onDelete: ReferentialAction.Cascade);
    }
}

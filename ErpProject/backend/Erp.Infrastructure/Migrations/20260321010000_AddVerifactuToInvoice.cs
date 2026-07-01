using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddVerifactuToInvoice : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "VerifactuHuella",
            table: "Invoices",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "VerifactuQrUrl",
            table: "Invoices",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "VerifactuHuella",
            table: "Invoices");

        migrationBuilder.DropColumn(
            name: "VerifactuQrUrl",
            table: "Invoices");
    }
}

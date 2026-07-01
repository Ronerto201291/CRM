using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddVerifactuSubmittedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<DateTime>(
            name: "VerifactuSubmittedAt",
            table: "Invoices",
            type: "timestamp with time zone",
            nullable: true,
            comment: "Timestamp del último envío exitoso a AEAT (Verifactu). Null = pendiente de enviar.");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "VerifactuSubmittedAt", table: "Invoices");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>
/// RGPD / LOPDGDD — Derecho de supresión (Art. 17 RGPD).
/// Añade campos IsAnonymized y AnonymizedAt a Clients, Suppliers y Contacts
/// para implementar pseudoanonimización sin borrar el registro fiscal.
/// </summary>
public partial class GdprAnonymizationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ── Clients ───────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymized",
            table: "Clients",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "AnonymizedAt",
            table: "Clients",
            type: "timestamp with time zone",
            nullable: true);

        // ── Suppliers ─────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymized",
            table: "Suppliers",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "AnonymizedAt",
            table: "Suppliers",
            type: "timestamp with time zone",
            nullable: true);

        // ── Contacts ──────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<bool>(
            name: "IsAnonymized",
            table: "Contacts",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "AnonymizedAt",
            table: "Contacts",
            type: "timestamp with time zone",
            nullable: true);

        // Índice para consultar rápidamente registros anonimizados
        migrationBuilder.CreateIndex(
            name: "IX_Clients_IsAnonymized",
            table: "Clients",
            column: "IsAnonymized");

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_IsAnonymized",
            table: "Suppliers",
            column: "IsAnonymized");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Clients_IsAnonymized",   table: "Clients");
        migrationBuilder.DropIndex(name: "IX_Suppliers_IsAnonymized", table: "Suppliers");

        migrationBuilder.DropColumn(name: "AnonymizedAt",  table: "Contacts");
        migrationBuilder.DropColumn(name: "IsAnonymized",  table: "Contacts");
        migrationBuilder.DropColumn(name: "AnonymizedAt",  table: "Suppliers");
        migrationBuilder.DropColumn(name: "IsAnonymized",  table: "Suppliers");
        migrationBuilder.DropColumn(name: "AnonymizedAt",  table: "Clients");
        migrationBuilder.DropColumn(name: "IsAnonymized",  table: "Clients");
    }
}

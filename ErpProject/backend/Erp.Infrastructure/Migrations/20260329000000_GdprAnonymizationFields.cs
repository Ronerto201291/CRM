using System;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>
/// RGPD / LOPDGDD — Derecho de supresión (Art. 17 RGPD).
/// Añade campos IsAnonymized y AnonymizedAt a Clients, Suppliers y Contacts
/// para implementar pseudoanonimización sin borrar el registro fiscal.
/// </summary>
[DbContext(typeof(ErpDbContext))]
[Migration("20260329000000_GdprAnonymizationFields")]
public partial class GdprAnonymizationFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Clients" ADD COLUMN IF NOT EXISTS "IsAnonymized" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Clients" ADD COLUMN IF NOT EXISTS "AnonymizedAt" timestamp with time zone;
            ALTER TABLE "Suppliers" ADD COLUMN IF NOT EXISTS "IsAnonymized" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Suppliers" ADD COLUMN IF NOT EXISTS "AnonymizedAt" timestamp with time zone;
            ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "IsAnonymized" boolean NOT NULL DEFAULT false;
            ALTER TABLE "Contacts" ADD COLUMN IF NOT EXISTS "AnonymizedAt" timestamp with time zone;
            CREATE INDEX IF NOT EXISTS "IX_Clients_IsAnonymized" ON "Clients" ("IsAnonymized");
            CREATE INDEX IF NOT EXISTS "IX_Suppliers_IsAnonymized" ON "Suppliers" ("IsAnonymized");
            """);
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

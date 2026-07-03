using System;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ErpDbContext))]
[Migration("20260417000000_AddVerifactuSubmittedAt")]
public partial class AddVerifactuSubmittedAt : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Invoices" ADD COLUMN IF NOT EXISTS "VerifactuSubmittedAt" timestamp with time zone;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "VerifactuSubmittedAt", table: "Invoices");
    }
}

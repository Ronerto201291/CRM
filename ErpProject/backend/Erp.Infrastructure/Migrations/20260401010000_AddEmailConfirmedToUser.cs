using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ErpDbContext))]
[Migration("20260401010000_AddEmailConfirmedToUser")]
public partial class AddEmailConfirmedToUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "EmailConfirmed" boolean NOT NULL DEFAULT false;
            UPDATE "Users" SET "EmailConfirmed" = TRUE WHERE "EmailConfirmed" = FALSE;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailConfirmed", table: "Users");
    }
}

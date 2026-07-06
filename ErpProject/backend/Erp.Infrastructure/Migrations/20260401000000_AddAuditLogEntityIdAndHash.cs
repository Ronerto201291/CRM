using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ErpDbContext))]
[Migration("20260401000000_AddAuditLogEntityIdAndHash")]
public partial class AddAuditLogEntityIdAndHash : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "AuditLogs" ADD COLUMN IF NOT EXISTS "EntityId" uuid;
            ALTER TABLE "AuditLogs" ADD COLUMN IF NOT EXISTS "Hash" text;
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EntityId", table: "AuditLogs");
        migrationBuilder.DropColumn(name: "Hash",     table: "AuditLogs");
    }
}

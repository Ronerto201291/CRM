using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
[DbContext(typeof(ErpDbContext))]
[Migration("20260705180000_AddProactiveNotificationsFrequency")]
public partial class AddProactiveNotificationsFrequency : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "ProactiveNotificationsFrequency" text NOT NULL DEFAULT 'daily';
            """);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Companies" DROP COLUMN IF EXISTS "ProactiveNotificationsFrequency";
            """);
    }
}

using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260704120000_AddMatchingToleranceAmountToCompany")]
public partial class AddMatchingToleranceAmountToCompany : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "MatchingToleranceAmount" numeric(18,4) NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "MatchingToleranceAmount",
            table: "Companies");
    }
}

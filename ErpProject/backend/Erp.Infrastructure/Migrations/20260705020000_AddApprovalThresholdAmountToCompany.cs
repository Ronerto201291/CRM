using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260705020000_AddApprovalThresholdAmountToCompany")]
public partial class AddApprovalThresholdAmountToCompany : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "ApprovalThresholdAmount" numeric(18,4) NOT NULL DEFAULT 0;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ApprovalThresholdAmount",
            table: "Companies");
    }
}

using System;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>ADR-0018 #42a fase 2 — unicidad de email por empresa, no global.</summary>
[DbContext(typeof(ErpDbContext))]
[Migration("20260703140000_AddUsersEmailCompanyIdUniqueIndex")]
public partial class AddUsersEmailCompanyIdUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Users_Email_CompanyId" ON "Users" ("Email", "CompanyId");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_Email_CompanyId", table: "Users");
    }
}

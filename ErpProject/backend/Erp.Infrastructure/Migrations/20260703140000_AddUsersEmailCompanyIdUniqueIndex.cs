using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>ADR-0018 #42a fase 2 — unicidad de email por empresa, no global.</summary>
public partial class AddUsersEmailCompanyIdUniqueIndex : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateIndex(
            name: "IX_Users_Email_CompanyId",
            table: "Users",
            columns: new[] { "Email", "CompanyId" },
            unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Users_Email_CompanyId", table: "Users");
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

public partial class AddUserCompanies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserCompanies",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                RoleId = table.Column<Guid>(type: "uuid", nullable: true),
                IsDefault = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserCompanies", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserCompanies_Companies_CompanyId",
                    column: x => x.CompanyId,
                    principalTable: "Companies",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserCompanies_Roles_RoleId",
                    column: x => x.RoleId,
                    principalTable: "Roles",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_UserCompanies_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_UserCompanies_CompanyId",
            table: "UserCompanies",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_UserCompanies_UserId_CompanyId",
            table: "UserCompanies",
            columns: new[] { "UserId", "CompanyId" },
            unique: true);

        migrationBuilder.Sql("""
            INSERT INTO "UserCompanies" ("Id", "UserId", "CompanyId", "RoleId", "IsDefault")
            SELECT gen_random_uuid(), "Id", "CompanyId", "RoleId", true
            FROM "Users"
            WHERE NOT EXISTS (
                SELECT 1 FROM "UserCompanies" uc WHERE uc."UserId" = "Users"."Id"
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "UserCompanies");
    }
}

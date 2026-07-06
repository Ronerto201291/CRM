using System;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260703120000_AddUserCompanies")]
public partial class AddUserCompanies : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UserCompanies" (
                "Id" uuid NOT NULL,
                "UserId" uuid NOT NULL,
                "CompanyId" uuid NOT NULL,
                "RoleId" uuid NULL,
                "IsDefault" boolean NOT NULL DEFAULT false,
                CONSTRAINT "PK_UserCompanies" PRIMARY KEY ("Id"),
                CONSTRAINT "FK_UserCompanies_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_UserCompanies_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE SET NULL,
                CONSTRAINT "FK_UserCompanies_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            CREATE INDEX IF NOT EXISTS "IX_UserCompanies_CompanyId" ON "UserCompanies" ("CompanyId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_UserCompanies_UserId_CompanyId" ON "UserCompanies" ("UserId", "CompanyId");
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

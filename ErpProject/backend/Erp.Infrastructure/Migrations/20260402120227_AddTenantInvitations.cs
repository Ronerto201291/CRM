using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantInvitations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                CREATE TABLE IF NOT EXISTS "TenantInvitations" (
                    "Id" uuid NOT NULL,
                    "CompanyId" uuid NOT NULL,
                    "Email" text NOT NULL,
                    "Token" text NOT NULL,
                    "ExpiresAt" timestamp with time zone NOT NULL,
                    "IsUsed" boolean NOT NULL,
                    CONSTRAINT "PK_TenantInvitations" PRIMARY KEY ("Id"),
                    CONSTRAINT "FK_TenantInvitations_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
                );
                CREATE INDEX IF NOT EXISTS "IX_TenantInvitations_CompanyId" ON "TenantInvitations" ("CompanyId");
                CREATE UNIQUE INDEX IF NOT EXISTS "IX_TenantInvitations_Token" ON "TenantInvitations" ("Token");
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TenantInvitations");
        }
    }
}

using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260705190000_AddPushSubscriptions")]
public partial class AddPushSubscriptions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
                "Id" uuid NOT NULL PRIMARY KEY,
                "UserId" uuid NOT NULL,
                "CompanyId" uuid NOT NULL,
                "Endpoint" text NOT NULL,
                "P256dh" text NOT NULL,
                "Auth" text NOT NULL,
                "CreatedAt" timestamp with time zone NOT NULL DEFAULT NOW()
            );
            CREATE INDEX IF NOT EXISTS "IX_PushSubscriptions_UserId" ON "PushSubscriptions" ("UserId");
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_PushSubscriptions_Endpoint" ON "PushSubscriptions" ("Endpoint");
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""DROP TABLE IF EXISTS "PushSubscriptions";""");
    }
}

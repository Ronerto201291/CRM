using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

[DbContext(typeof(ErpDbContext))]
[Migration("20260703140000_AddStripeWebhookEvents")]
public partial class AddStripeWebhookEvents : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "StripeWebhookEvents" (
                "EventId" character varying(255) NOT NULL,
                "EventType" character varying(100) NOT NULL,
                "ProcessedAt" timestamp with time zone NOT NULL,
                CONSTRAINT "PK_StripeWebhookEvents" PRIMARY KEY ("EventId")
            );
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "StripeWebhookEvents");
    }
}

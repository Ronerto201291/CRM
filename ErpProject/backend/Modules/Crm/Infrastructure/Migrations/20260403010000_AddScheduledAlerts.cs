using System;
using Erp.Modules.Crm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CrmDbContext))]
    [Migration("20260403010000_AddScheduledAlerts")]
    public partial class AddScheduledAlerts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ScheduledAlerts",
                schema: "crm",
                columns: table => new
                {
                    Id             = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId      = table.Column<Guid>(type: "uuid", nullable: false),
                    Title          = table.Column<string>(type: "text", nullable: false),
                    Description    = table.Column<string>(type: "text", nullable: true),
                    ScheduledAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClientId       = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName     = table.Column<string>(type: "text", nullable: true),
                    IsAcknowledged = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SnoozedUntil   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScheduledAlerts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ScheduledAlerts_CompanyId_ScheduledAt",
                schema: "crm",
                table: "ScheduledAlerts",
                columns: new[] { "CompanyId", "ScheduledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ScheduledAlerts",
                schema: "crm");
        }
    }
}

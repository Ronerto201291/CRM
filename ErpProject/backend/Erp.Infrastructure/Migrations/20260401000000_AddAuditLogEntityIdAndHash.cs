using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddAuditLogEntityIdAndHash : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "EntityId",
            table: "AuditLogs",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Hash",
            table: "AuditLogs",
            type: "text",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EntityId", table: "AuditLogs");
        migrationBuilder.DropColumn(name: "Hash",     table: "AuditLogs");
    }
}

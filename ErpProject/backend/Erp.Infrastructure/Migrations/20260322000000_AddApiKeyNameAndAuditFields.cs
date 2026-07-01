using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddApiKeyNameAndAuditFields : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "ApiKeys",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "ApiKeys",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "now()");

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "ApiKeys",
            type: "timestamp with time zone",
            nullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "Name",      table: "ApiKeys");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "ApiKeys");
        migrationBuilder.DropColumn(name: "UpdatedAt", table: "ApiKeys");
    }
}

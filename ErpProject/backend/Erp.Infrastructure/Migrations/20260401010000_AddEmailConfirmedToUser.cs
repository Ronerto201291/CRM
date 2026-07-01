using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddEmailConfirmedToUser : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<bool>(
            name: "EmailConfirmed",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        // Mark existing users as confirmed so existing tenants are not blocked.
        migrationBuilder.Sql("UPDATE \"Users\" SET \"EmailConfirmed\" = TRUE;");
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "EmailConfirmed", table: "Users");
    }
}

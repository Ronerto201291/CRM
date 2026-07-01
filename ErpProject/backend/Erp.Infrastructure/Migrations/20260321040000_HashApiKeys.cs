using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>
/// Removes plaintext Key from ApiKeys; adds KeyPrefix (first 8 chars) + KeyHash (SHA-256 hex).
/// Existing keys are migrated: KeyPrefix = first 8 chars, KeyHash computed in SQL, plaintext dropped.
/// IMPORTANT: All existing API keys are invalidated — issue new ones after deploying this migration.
/// </summary>
public partial class HashApiKeys : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Add new columns (nullable during migration)
        migrationBuilder.AddColumn<string>(
            name: "KeyPrefix",
            table: "ApiKeys",
            type: "character varying(16)",
            maxLength: 16,
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "KeyHash",
            table: "ApiKeys",
            type: "character varying(64)",
            maxLength: 64,
            nullable: false,
            defaultValue: "");

        // 2. Populate from existing plaintext Key column (requires pgcrypto extension for sha256)
        //    encode(sha256("Key"::bytea), 'hex') produces the same hex digest as .NET's SHA256 + Convert.ToHexString
        migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS pgcrypto;");
        migrationBuilder.Sql("""
            UPDATE "ApiKeys"
            SET "KeyPrefix" = left("Key", 8),
                "KeyHash"   = encode(digest("Key", 'sha256'), 'hex');
            """);

        // 3. Add unique index on KeyHash for fast lookup
        migrationBuilder.CreateIndex(
            name: "IX_ApiKeys_KeyHash",
            table: "ApiKeys",
            column: "KeyHash",
            unique: true);

        // 4. Drop the plaintext Key column — never store it again
        migrationBuilder.DropColumn(name: "Key", table: "ApiKeys");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Key",
            table: "ApiKeys",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.DropIndex(name: "IX_ApiKeys_KeyHash", table: "ApiKeys");
        migrationBuilder.DropColumn(name: "KeyHash", table: "ApiKeys");
        migrationBuilder.DropColumn(name: "KeyPrefix", table: "ApiKeys");
    }
}

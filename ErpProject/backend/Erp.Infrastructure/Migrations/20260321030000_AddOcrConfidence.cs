using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddOcrConfidence : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // No-op: ExpenseDocuments table is created in 20260322000000_CompleteSchema
        // with OcrConfidence already included as a column.
        // This migration stub is kept to preserve the migration chain ID.
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // No-op: column is removed when ExpenseDocuments table is dropped in CompleteSchema.Down()
    }
}

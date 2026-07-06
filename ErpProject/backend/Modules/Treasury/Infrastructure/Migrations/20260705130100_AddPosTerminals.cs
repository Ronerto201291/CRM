using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Treasury.Infrastructure.Migrations;

public partial class AddPosTerminals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "PosTerminals",
            schema: "treasury",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "text", nullable: false),
                TerminalCode = table.Column<string>(type: "text", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                LastPaymentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PosTerminals", x => x.Id));

        migrationBuilder.CreateTable(
            name: "PosPayments",
            schema: "treasury",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                PosTerminalId = table.Column<Guid>(type: "uuid", nullable: false),
                InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                CurrencyCode = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false),
                ExternalReference = table.Column<string>(type: "text", nullable: true),
                PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_PosPayments", x => x.Id));

        migrationBuilder.CreateIndex(
            name: "IX_PosTerminals_CompanyId",
            schema: "treasury",
            table: "PosTerminals",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_PosPayments_CompanyId",
            schema: "treasury",
            table: "PosPayments",
            column: "CompanyId");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "PosPayments", schema: "treasury");
        migrationBuilder.DropTable(name: "PosTerminals", schema: "treasury");
    }
}

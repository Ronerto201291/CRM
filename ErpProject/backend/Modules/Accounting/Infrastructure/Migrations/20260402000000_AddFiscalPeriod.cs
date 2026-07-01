using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Accounting.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFiscalPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FiscalPeriods",
                schema: "accounting",
                columns: table => new
                {
                    Id                    = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId             = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear            = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt              = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedByUserId        = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosingJournalEntryId = table.Column<Guid>(type: "uuid", nullable: false),
                    ResultadoNeto         = table.Column<decimal>(type: "numeric", nullable: false),
                    Notes                 = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FiscalPeriods", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FiscalPeriods_CompanyId_FiscalYear",
                schema: "accounting",
                table: "FiscalPeriods",
                columns: new[] { "CompanyId", "FiscalYear" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FiscalPeriods",
                schema: "accounting");
        }
    }
}

using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Payroll.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PayrollSettlementJournalEntry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "JournalEntryId",
                schema: "payroll",
                table: "PayrollSettlements",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "JournalEntryId",
                schema: "payroll",
                table: "PayrollSettlements");
        }
    }
}

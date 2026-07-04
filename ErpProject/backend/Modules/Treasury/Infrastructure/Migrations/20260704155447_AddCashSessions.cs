using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Treasury.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCashSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "treasury",
                table: "ReconciliationBatches",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Auto");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Draft");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentType",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Supplier");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "EUR");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                schema: "treasury",
                table: "CashFlowForecasts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Manual");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Pending");

            migrationBuilder.AlterColumn<string>(
                name: "ClientAccountCode",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "4300");

            migrationBuilder.AlterColumn<string>(
                name: "AccountingAccountCode",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "4310");

            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                schema: "treasury",
                table: "BankMovements",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "Manual");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "treasury",
                table: "BankAccounts",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: true);

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "treasury",
                table: "BankAccounts",
                type: "text",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "text",
                oldDefaultValue: "EUR");
            migrationBuilder.CreateTable(
                name: "CashSessions",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    OpeningBalance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedByUserId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpectedClosingBalance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    CountedClosingBalance = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Difference = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashSessions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CashSessions_CompanyId_Status",
                schema: "treasury",
                table: "CashSessions",
                columns: new[] { "CompanyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CashSessions",
                schema: "treasury");

            migrationBuilder.AlterColumn<string>(
                name: "Type",
                schema: "treasury",
                table: "ReconciliationBatches",
                type: "text",
                nullable: false,
                defaultValue: "Auto",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                defaultValue: "Draft",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "PaymentType",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                defaultValue: "Supplier",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "treasury",
                table: "PaymentOrders",
                type: "text",
                nullable: false,
                defaultValue: "EUR",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Source",
                schema: "treasury",
                table: "CashFlowForecasts",
                type: "text",
                nullable: false,
                defaultValue: "Manual",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                defaultValue: "Pending",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "ClientAccountCode",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                defaultValue: "4300",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "AccountingAccountCode",
                schema: "treasury",
                table: "CashEffects",
                type: "text",
                nullable: false,
                defaultValue: "4310",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<string>(
                name: "Origin",
                schema: "treasury",
                table: "BankMovements",
                type: "text",
                nullable: false,
                defaultValue: "Manual",
                oldClrType: typeof(string),
                oldType: "text");

            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                schema: "treasury",
                table: "BankAccounts",
                type: "boolean",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "boolean");

            migrationBuilder.AlterColumn<string>(
                name: "CurrencyCode",
                schema: "treasury",
                table: "BankAccounts",
                type: "text",
                nullable: false,
                defaultValue: "EUR",
                oldClrType: typeof(string),
                oldType: "text");
        }
    }
}

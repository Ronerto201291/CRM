using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase0InvoiceCompliance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ClientViesConsultedAtUtc",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientViesCountryCode",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ClientViesName",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ClientViesValid",
                schema: "billing",
                table: "Invoices",
                type: "boolean",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RectificationPeriodFrom",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RectificationPeriodTo",
                schema: "billing",
                table: "Invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RectificationReasonCode",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RectificationReasonText",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ClientViesConsultedAtUtc",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClientViesCountryCode",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClientViesName",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "ClientViesValid",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RectificationPeriodFrom",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RectificationPeriodTo",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RectificationReasonCode",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "RectificationReasonText",
                schema: "billing",
                table: "Invoices");
        }
    }
}

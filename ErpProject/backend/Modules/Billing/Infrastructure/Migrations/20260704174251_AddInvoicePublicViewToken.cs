using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoicePublicViewToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PublicViewToken",
                schema: "billing",
                table: "Invoices",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Backfill: AddColumn above gives every existing invoice the same default
            // value (""), which would make the unique index below fail immediately on
            // any database with more than one invoice already in it. Assign each
            // existing row a distinct token before the index is created.
            migrationBuilder.Sql(@"
                UPDATE billing.""Invoices"" SET ""PublicViewToken"" = replace(gen_random_uuid()::text, '-', '');
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Invoices_PublicViewToken",
                schema: "billing",
                table: "Invoices",
                column: "PublicViewToken",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Invoices_PublicViewToken",
                schema: "billing",
                table: "Invoices");

            migrationBuilder.DropColumn(
                name: "PublicViewToken",
                schema: "billing",
                table: "Invoices");
        }
    }
}

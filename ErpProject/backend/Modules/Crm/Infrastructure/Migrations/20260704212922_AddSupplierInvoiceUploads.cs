using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupplierInvoiceUploads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PublicUploadEnabled",
                schema: "crm",
                table: "Suppliers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PublicUploadToken",
                schema: "crm",
                table: "Suppliers",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "SupplierInvoiceUploads",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicTokenUsed = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupplierInvoiceUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupplierInvoiceUploads_Suppliers_SupplierId",
                        column: x => x.SupplierId,
                        principalSchema: "crm",
                        principalTable: "Suppliers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // Backfill: AddColumn deja el mismo valor por defecto ("") en todas las filas
            // existentes, lo que haría fallar el índice único de abajo en cualquier base
            // con más de un proveedor ya creado (mismo bug ya corregido en
            // AddInvoicePublicViewToken, ADR-0018 #39).
            migrationBuilder.Sql(@"UPDATE crm.""Suppliers"" SET ""PublicUploadToken"" = replace(gen_random_uuid()::text, '-', '');");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_PublicUploadToken",
                schema: "crm",
                table: "Suppliers",
                column: "PublicUploadToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceUploads_CompanyId",
                schema: "crm",
                table: "SupplierInvoiceUploads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierInvoiceUploads_SupplierId",
                schema: "crm",
                table: "SupplierInvoiceUploads",
                column: "SupplierId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupplierInvoiceUploads",
                schema: "crm");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_PublicUploadToken",
                schema: "crm",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PublicUploadEnabled",
                schema: "crm",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "PublicUploadToken",
                schema: "crm",
                table: "Suppliers");
        }
    }
}

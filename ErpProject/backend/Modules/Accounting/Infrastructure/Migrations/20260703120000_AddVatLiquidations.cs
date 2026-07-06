using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Accounting.Infrastructure.Migrations
{
    public partial class AddVatLiquidations : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VatLiquidations",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Quarter = table.Column<int>(type: "integer", nullable: false),
                    ModeloCode = table.Column<string>(type: "text", nullable: false),
                    TotalDevengado = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IvaDeducible = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Resultado = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ResultadoTipo = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatLiquidations", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VatLiquidations_CompanyId_Year_Quarter",
                schema: "accounting",
                table: "VatLiquidations",
                columns: new[] { "CompanyId", "Year", "Quarter" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VatLiquidations", schema: "accounting");
        }
    }
}

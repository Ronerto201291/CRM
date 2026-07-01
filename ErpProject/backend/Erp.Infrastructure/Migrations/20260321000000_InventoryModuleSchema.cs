using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <summary>
    /// Sprint 2 — Inventory Module Schema.
    /// Replaces the legacy Products / StockMovements tables with the modular inventory schema.
    /// Creates: InventoryProducts, Warehouses, Stocks, new StockMovements, TenantModules.
    /// </summary>
    public partial class InventoryModuleSchema : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ─── Drop legacy tables (old non-modular schema) ──────────────────
            migrationBuilder.DropTable(name: "StockMovements");
            migrationBuilder.DropTable(name: "Products");

            // ─── InventoryProducts ────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "InventoryProducts",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId   = table.Column<Guid>(type: "uuid", nullable: false),
                    SKU         = table.Column<string>(type: "text", nullable: false),
                    Name        = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Type        = table.Column<string>(type: "text", nullable: false),
                    CostPrice   = table.Column<decimal>(type: "numeric", nullable: false),
                    SalePrice   = table.Column<decimal>(type: "numeric", nullable: false),
                    VatPercent  = table.Column<decimal>(type: "numeric", nullable: false),
                    TrackStock  = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive    = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryProducts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryProducts_CompanyId",
                table: "InventoryProducts",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UQ_InventoryProducts_CompanyId_SKU",
                table: "InventoryProducts",
                columns: new[] { "CompanyId", "SKU" },
                unique: true);

            // ─── Warehouses ───────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Warehouses",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name      = table.Column<string>(type: "text", nullable: false),
                    Location  = table.Column<string>(type: "text", nullable: false),
                    IsActive  = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Warehouses", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_CompanyId",
                table: "Warehouses",
                column: "CompanyId");

            // ─── Stocks ───────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "Stocks",
                columns: table => new
                {
                    Id          = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId   = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId   = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId = table.Column<Guid>(type: "uuid", nullable: false),
                    Quantity    = table.Column<decimal>(type: "numeric", nullable: false),
                    CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Stocks", x => x.Id);
                    table.CheckConstraint("CK_Stock_Quantity_NonNegative", "\"Quantity\" >= 0");
                    table.ForeignKey(
                        name: "FK_Stocks_InventoryProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "InventoryProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Stocks_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Stocks_CompanyId",
                table: "Stocks",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "UQ_Stocks_Product_Warehouse",
                table: "Stocks",
                columns: new[] { "ProductId", "WarehouseId" },
                unique: true);

            // ─── StockMovements (new modular schema) ──────────────────────────
            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id            = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId     = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId     = table.Column<Guid>(type: "uuid", nullable: false),
                    WarehouseId   = table.Column<Guid>(type: "uuid", nullable: false),
                    MovementType  = table.Column<string>(type: "text", nullable: false),
                    Quantity      = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitCost      = table.Column<decimal>(type: "numeric", nullable: false),
                    ReferenceType = table.Column<string>(type: "text", nullable: false),
                    ReferenceId   = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt     = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_InventoryProducts_ProductId",
                        column: x => x.ProductId,
                        principalTable: "InventoryProducts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_StockMovements_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_CompanyId",
                table: "StockMovements",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_ProductId",
                table: "StockMovements",
                column: "ProductId");

            // Immutability trigger: prevent UPDATE and DELETE on StockMovements
            migrationBuilder.Sql(@"
                CREATE OR REPLACE FUNCTION prevent_stockmovement_mutation()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'StockMovements are immutable (Ley 11/2021 Antifraude)';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_no_update_stockmovements
                BEFORE UPDATE OR DELETE ON public.""StockMovements""
                FOR EACH ROW EXECUTE FUNCTION prevent_stockmovement_mutation();
            ");

            // ─── TenantModules ────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "TenantModules",
                columns: table => new
                {
                    Id         = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId  = table.Column<Guid>(type: "uuid", nullable: false),
                    ModuleName = table.Column<string>(type: "text", nullable: false),
                    IsEnabled  = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantModules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantModules_Companies_CompanyId",
                        column: x => x.CompanyId,
                        principalTable: "Companies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "UQ_TenantModules_Company_Module",
                table: "TenantModules",
                columns: new[] { "CompanyId", "ModuleName" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "TenantModules");
            migrationBuilder.DropTable(name: "StockMovements");
            migrationBuilder.DropTable(name: "Stocks");
            migrationBuilder.DropTable(name: "Warehouses");
            migrationBuilder.DropTable(name: "InventoryProducts");

            // Restore legacy schema (bare minimum for rollback)
            migrationBuilder.CreateTable(
                name: "Products",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name      = table.Column<string>(type: "text", nullable: false),
                    SKU       = table.Column<string>(type: "text", nullable: false),
                    Price     = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate   = table.Column<decimal>(type: "numeric", nullable: false),
                    Stock     = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table => table.PrimaryKey("PK_Products", x => x.Id));

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id        = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type      = table.Column<string>(type: "text", nullable: false),
                    Quantity  = table.Column<decimal>(type: "numeric", nullable: false),
                    Date      = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });
        }
    }
}

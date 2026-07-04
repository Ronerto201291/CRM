using System;
using Erp.Modules.Accounting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Accounting.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(AccountingDbContext))]
    [Migration("20260402020000_AddAmortizationsAndDeferredEntries")]
    public partial class AddAmortizationsAndDeferredEntries : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ── FixedAssets ───────────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "FixedAssets",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssetCode = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CommissioningDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcquisitionCost = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    ResidualValue = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    UsefulLifeYears = table.Column<int>(type: "integer", nullable: false),
                    AmortizationMethod = table.Column<string>(type: "text", nullable: false, defaultValue: "Lineal"),
                    AssetAccountCode = table.Column<string>(type: "text", nullable: false, defaultValue: "213"),
                    DepreciationAccountCode = table.Column<string>(type: "text", nullable: false, defaultValue: "681"),
                    AccumDepreciationAccountCode = table.Column<string>(type: "text", nullable: false, defaultValue: "281"),
                    AccumulatedDepreciation = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    LastAmortizationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    DisposedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FixedAssets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId",
                schema: "accounting",
                table: "FixedAssets",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_FixedAssets_CompanyId_AssetCode",
                schema: "accounting",
                table: "FixedAssets",
                columns: new[] { "CompanyId", "AssetCode" },
                unique: true);

            // ── DeferredEntries ───────────────────────────────────────────────────────
            migrationBuilder.CreateTable(
                name: "DeferredEntries",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    EntryType = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false),
                    PeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecognizedAmount = table.Column<decimal>(type: "numeric(18,2)", precision: 18, scale: 2, nullable: false, defaultValue: 0m),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Active"),
                    DeferralAccountCode = table.Column<string>(type: "text", nullable: false),
                    CounterpartAccountCode = table.Column<string>(type: "text", nullable: false),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeferredEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeferredEntries_CompanyId",
                schema: "accounting",
                table: "DeferredEntries",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_DeferredEntries_Status",
                schema: "accounting",
                table: "DeferredEntries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "DeferredEntries", schema: "accounting");
            migrationBuilder.DropTable(name: "FixedAssets", schema: "accounting");
        }
    }
}

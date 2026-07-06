using System;
using Erp.Modules.Accounting.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Accounting.Infrastructure.Migrations
{
    [DbContext(typeof(AccountingDbContext))]
    [Migration("20260425000000_Phase3VatAndFiscality")]
    public partial class Phase3VatAndFiscality : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "VatRegimes",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EffectiveDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatRegimes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "VatTransactions",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseId = table.Column<Guid>(type: "uuid", nullable: true),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Direction = table.Column<string>(type: "text", nullable: false),
                    VatRegime = table.Column<string>(type: "text", nullable: false),
                    VatableBase = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DeductibleVat = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsRecognizedByCashBasis = table.Column<bool>(type: "boolean", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VatTransactions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProrrataCalculations",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    InlandRevenue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExemptRevenue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ProrrataPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AdjustmentAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProrrataCalculations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ViesDeclarations",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CountryCode = table.Column<string>(type: "text", nullable: false),
                    VatNumber = table.Column<string>(type: "text", nullable: false),
                    TotalAmountSupplies = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalAmountServices = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DeclarationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsValid = table.Column<bool>(type: "boolean", nullable: false),
                    ValidationStatus = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ViesDeclarations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "RecargosDEquivalencia",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierVatNumber = table.Column<string>(type: "text", nullable: false),
                    SupplierIsRE = table.Column<bool>(type: "boolean", nullable: false),
                    Base = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    RechargeRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    RechargeAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsEndToEnd = table.Column<bool>(type: "boolean", nullable: false),
                    Modelo303Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RecargosDEquivalencia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "InversionesDeSubjetoActivo",
                schema: "accounting",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SupplierCountryCode = table.Column<string>(type: "text", nullable: false),
                    VatableBase = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    VatRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    VatAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    IsReverseCharge = table.Column<bool>(type: "boolean", nullable: false),
                    DeclarationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InversionesDeSubjetoActivo", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VatRegimes_CompanyId",
                schema: "accounting",
                table: "VatRegimes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_VatTransactions_CompanyId_Direction",
                schema: "accounting",
                table: "VatTransactions",
                columns: new[] { "CompanyId", "Direction" });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "VatRegimes", schema: "accounting");
            migrationBuilder.DropTable(name: "VatTransactions", schema: "accounting");
            migrationBuilder.DropTable(name: "ProrrataCalculations", schema: "accounting");
            migrationBuilder.DropTable(name: "ViesDeclarations", schema: "accounting");
            migrationBuilder.DropTable(name: "RecargosDEquivalencia", schema: "accounting");
            migrationBuilder.DropTable(name: "InversionesDeSubjetoActivo", schema: "accounting");
        }
    }
}

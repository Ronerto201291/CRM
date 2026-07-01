using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Treasury.Infrastructure.Migrations
{
    public partial class Phase4TreasuryFinancingGroups : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 4.1 - CURRENCIES
            migrationBuilder.CreateTable(
                name: "Currencies",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Currencies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CurrencyExchanges",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromCurrency = table.Column<string>(type: "text", nullable: false),
                    ToCurrency = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExchangedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    ExchangeDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    LinkedTransactionId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CurrencyExchanges", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExchangeRateHistories",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    Rate = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    RateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateHistories", x => x.Id);
                });

            // 4.2 - FINANCING
            migrationBuilder.CreateTable(
                name: "ConfirmingOperations",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AdvancePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FinancingProvider = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfirmingOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FactoringOperations",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AdvancePercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    DiscountFee = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CreationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PaymentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    FactoringProvider = table.Column<string>(type: "text", nullable: false),
                    IsWithRecourse = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FactoringOperations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "FinancingAccounts",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Limit = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UtilizedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Provider = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FinancingAccounts", x => x.Id);
                });

            // 4.3 - GUARANTEES
            migrationBuilder.CreateTable(
                name: "Guarantees",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    RelatedEntity = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ClaimDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guarantees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankGuarantees",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeNumber = table.Column<string>(type: "text", nullable: false),
                    Bank = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "text", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(5,3)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankGuarantees", x => x.Id);
                });

            // 4.4 - CONSOLIDATION
            migrationBuilder.CreateTable(
                name: "ConsolidationGroups",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ConsolidationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Method = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidationGroups", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SubsidiaryCompanies",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    VotingPercentage = table.Column<decimal>(type: "numeric(5,2)", nullable: false),
                    ConsolidationMethod = table.Column<string>(type: "text", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcquisitionPrice = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubsidiaryCompanies", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsolidationAdjustments",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Account = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    AdjustmentDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidationAdjustments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConsolidatedFinancialStatements",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    StatementType = table.Column<string>(type: "text", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    NetIncome = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalAssets = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    TotalEquity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    PreparedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConsolidatedFinancialStatements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IntercompanyTransactions",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SubsidiaryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Currency = table.Column<string>(type: "text", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsEliminated = table.Column<bool>(type: "boolean", nullable: false),
                    RelatedInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntercompanyTransactions", x => x.Id);
                });

            // Indexes
            migrationBuilder.CreateIndex(
                name: "IX_Currencies_CompanyId",
                schema: "treasury",
                table: "Currencies",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmingOperations_CompanyId_Status",
                schema: "treasury",
                table: "ConfirmingOperations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoringOperations_CompanyId_Status",
                schema: "treasury",
                table: "FactoringOperations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Guarantees_CompanyId_Status",
                schema: "treasury",
                table: "Guarantees",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationGroups_ParentCompanyId",
                schema: "treasury",
                table: "ConsolidationGroups",
                column: "ParentCompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_SubsidiaryCompanies_ParentCompanyId",
                schema: "treasury",
                table: "SubsidiaryCompanies",
                column: "ParentCompanyId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "Currencies", schema: "treasury");
            migrationBuilder.DropTable(name: "CurrencyExchanges", schema: "treasury");
            migrationBuilder.DropTable(name: "ExchangeRateHistories", schema: "treasury");
            migrationBuilder.DropTable(name: "ConfirmingOperations", schema: "treasury");
            migrationBuilder.DropTable(name: "FactoringOperations", schema: "treasury");
            migrationBuilder.DropTable(name: "FinancingAccounts", schema: "treasury");
            migrationBuilder.DropTable(name: "Guarantees", schema: "treasury");
            migrationBuilder.DropTable(name: "BankGuarantees", schema: "treasury");
            migrationBuilder.DropTable(name: "ConsolidationGroups", schema: "treasury");
            migrationBuilder.DropTable(name: "SubsidiaryCompanies", schema: "treasury");
            migrationBuilder.DropTable(name: "ConsolidationAdjustments", schema: "treasury");
            migrationBuilder.DropTable(name: "ConsolidatedFinancialStatements", schema: "treasury");
            migrationBuilder.DropTable(name: "IntercompanyTransactions", schema: "treasury");
        }
    }
}

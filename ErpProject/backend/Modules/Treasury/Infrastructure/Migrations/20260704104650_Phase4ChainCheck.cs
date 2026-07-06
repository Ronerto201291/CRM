using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Treasury.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase4ChainCheck : Migration
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
                name: "BankGuarantees",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    GuaranteeNumber = table.Column<string>(type: "text", nullable: false),
                    Bank = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    IssuedDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BeneficiaryId = table.Column<Guid>(type: "uuid", nullable: true),
                    BeneficiaryName = table.Column<string>(type: "text", nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankGuarantees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Collaterals",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Value = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ValuationDate = table.Column<DateTime>(type: "timestamp(18) with time zone", precision: 18, scale: 4, nullable: false),
                    LinkedAccount = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LTVRatio = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Collaterals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ConfirmingOperations",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uuid", nullable: false),
                    InvoiceAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdvancePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Fee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                name: "ConsolidatedFinancialStatements",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    StatementType = table.Column<string>(type: "text", nullable: false),
                    TotalRevenue = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalExpenses = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetIncome = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalAssets = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalLiabilities = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    TotalEquity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                name: "ConsolidationAdjustments",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationGroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Account = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                name: "ConsolidationGroups",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsolidationPercentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
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
                name: "Currencies",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
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
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExchangedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ExchangeRate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
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
                    Rate = table.Column<decimal>(type: "numeric(18,8)", precision: 18, scale: 8, nullable: false),
                    RateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExchangeRateHistories", x => x.Id);
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
                    InvoiceAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    AdvancePercentage = table.Column<decimal>(type: "numeric", nullable: false),
                    AdvanceAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    DiscountFee = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommissionAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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
                    Limit = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    UtilizedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    InterestRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "Guarantees",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ReferenceNumber = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false),
                    RelatedEntity = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiryDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ClaimedAmount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    ClaimDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Guarantees", x => x.Id);
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
                    Amount = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
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

            migrationBuilder.CreateTable(
                name: "SubsidiaryCompanies",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ParentCompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnershipPercentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    VotingPercentage = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    ConsolidationMethod = table.Column<string>(type: "text", nullable: false),
                    AcquisitionDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcquisitionPrice = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    DisposalDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubsidiaryCompanies", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BankGuarantees_CompanyId_Status",
                schema: "treasury",
                table: "BankGuarantees",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Collaterals_CompanyId",
                schema: "treasury",
                table: "Collaterals",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmingOperations_CompanyId_Status",
                schema: "treasury",
                table: "ConfirmingOperations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidatedFinancialStatements_ConsolidationGroupId_Fiscal~",
                schema: "treasury",
                table: "ConsolidatedFinancialStatements",
                columns: new[] { "ConsolidationGroupId", "FiscalYear", "StatementType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationAdjustments_ConsolidationGroupId",
                schema: "treasury",
                table: "ConsolidationAdjustments",
                column: "ConsolidationGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ConsolidationGroups_ParentCompanyId_Code",
                schema: "treasury",
                table: "ConsolidationGroups",
                columns: new[] { "ParentCompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Currencies_CompanyId_Code",
                schema: "treasury",
                table: "Currencies",
                columns: new[] { "CompanyId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CurrencyExchanges_CompanyId_ExchangeDate",
                schema: "treasury",
                table: "CurrencyExchanges",
                columns: new[] { "CompanyId", "ExchangeDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ExchangeRateHistories_CompanyId_CurrencyCode_RateDate",
                schema: "treasury",
                table: "ExchangeRateHistories",
                columns: new[] { "CompanyId", "CurrencyCode", "RateDate" });

            migrationBuilder.CreateIndex(
                name: "IX_FactoringOperations_CompanyId_Status",
                schema: "treasury",
                table: "FactoringOperations",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_FinancingAccounts_CompanyId_Status",
                schema: "treasury",
                table: "FinancingAccounts",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Guarantees_CompanyId_Status",
                schema: "treasury",
                table: "Guarantees",
                columns: new[] { "CompanyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_IntercompanyTransactions_ParentCompanyId_TransactionDate",
                schema: "treasury",
                table: "IntercompanyTransactions",
                columns: new[] { "ParentCompanyId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_SubsidiaryCompanies_ParentCompanyId_CompanyId",
                schema: "treasury",
                table: "SubsidiaryCompanies",
                columns: new[] { "ParentCompanyId", "CompanyId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BankGuarantees",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "Collaterals",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "ConfirmingOperations",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "ConsolidatedFinancialStatements",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "ConsolidationAdjustments",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "ConsolidationGroups",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "Currencies",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "CurrencyExchanges",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "ExchangeRateHistories",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "FactoringOperations",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "FinancingAccounts",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "Guarantees",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "IntercompanyTransactions",
                schema: "treasury");

            migrationBuilder.DropTable(
                name: "SubsidiaryCompanies",
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

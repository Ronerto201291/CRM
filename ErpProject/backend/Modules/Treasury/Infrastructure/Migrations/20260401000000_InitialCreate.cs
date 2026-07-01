using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Treasury.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "treasury");

            migrationBuilder.CreateTable(
                name: "BankAccounts",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Iban = table.Column<string>(type: "text", nullable: false),
                    BIC = table.Column<string>(type: "text", nullable: true),
                    BankName = table.Column<string>(type: "text", nullable: false),
                    CurrentBalance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    AccountingAccountCode = table.Column<string>(type: "text", nullable: true),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false, defaultValue: "EUR"),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankAccounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CashFlowForecasts",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ForecastDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpectedInflow = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpectedOutflow = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    ExpectedBalance = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Source = table.Column<string>(type: "text", nullable: false, defaultValue: "Manual"),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsActual = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashFlowForecasts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PaymentOrders",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PaymentType = table.Column<string>(type: "text", nullable: false, defaultValue: "Supplier"),
                    BeneficiaryName = table.Column<string>(type: "text", nullable: false),
                    BeneficiaryTaxId = table.Column<string>(type: "text", nullable: false),
                    BeneficiaryIban = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    CurrencyCode = table.Column<string>(type: "text", nullable: false, defaultValue: "EUR"),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    ScheduledDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExecutedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GeneratedBankMovementId = table.Column<Guid>(type: "uuid", nullable: true),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceType = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentOrders", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ReconciliationBatches",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReconciledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ItemsCount = table.Column<int>(type: "integer", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false, defaultValue: "Auto"),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReconciliationBatches", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BankMovements",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reference = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    ReconciliationBatchId = table.Column<Guid>(type: "uuid", nullable: true),
                    IsReconciled = table.Column<bool>(type: "boolean", nullable: false),
                    MatchedJournalEntryLineId = table.Column<Guid>(type: "uuid", nullable: true),
                    Origin = table.Column<string>(type: "text", nullable: false, defaultValue: "Manual"),
                    OriginalBankRef = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BankMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BankMovements_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "treasury",
                        principalTable: "BankAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BankMovements_ReconciliationBatches_ReconciliationBatchId",
                        column: x => x.ReconciliationBatchId,
                        principalSchema: "treasury",
                        principalTable: "ReconciliationBatches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CashEffects",
                schema: "treasury",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientName = table.Column<string>(type: "text", nullable: false),
                    ClientTaxId = table.Column<string>(type: "text", nullable: false),
                    EffectNumber = table.Column<string>(type: "text", nullable: false),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                    AccountingAccountCode = table.Column<string>(type: "text", nullable: false, defaultValue: "4310"),
                    ClientAccountCode = table.Column<string>(type: "text", nullable: false, defaultValue: "4300"),
                    BankAccountId = table.Column<Guid>(type: "uuid", nullable: true),
                    SEPAXml = table.Column<string>(type: "text", nullable: true),
                    SEPADownloadUrl = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashEffects", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashEffects_BankAccounts_BankAccountId",
                        column: x => x.BankAccountId,
                        principalSchema: "treasury",
                        principalTable: "BankAccounts",
                        principalColumn: "Id");
                });

            // Indexes
            migrationBuilder.CreateIndex(
                name: "IX_BankAccounts_CompanyId_Iban",
                schema: "treasury",
                table: "BankAccounts",
                columns: new[] { "CompanyId", "Iban" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_BankAccountId",
                schema: "treasury",
                table: "BankMovements",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_CompanyId_BankAccountId_Date",
                schema: "treasury",
                table: "BankMovements",
                columns: new[] { "CompanyId", "BankAccountId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_BankMovements_ReconciliationBatchId",
                schema: "treasury",
                table: "BankMovements",
                column: "ReconciliationBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_CashEffects_BankAccountId",
                schema: "treasury",
                table: "CashEffects",
                column: "BankAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_CashEffects_CompanyId_DueDate",
                schema: "treasury",
                table: "CashEffects",
                columns: new[] { "CompanyId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_CashFlowForecasts_CompanyId_ForecastDate",
                schema: "treasury",
                table: "CashFlowForecasts",
                columns: new[] { "CompanyId", "ForecastDate" });

            migrationBuilder.CreateIndex(
                name: "IX_PaymentOrders_CompanyId_ScheduledDate",
                schema: "treasury",
                table: "PaymentOrders",
                columns: new[] { "CompanyId", "ScheduledDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ReconciliationBatches_CompanyId_BankAccountId_ReconciledAt",
                schema: "treasury",
                table: "ReconciliationBatches",
                columns: new[] { "CompanyId", "BankAccountId", "ReconciledAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BankMovements", schema: "treasury");
            migrationBuilder.DropTable(name: "CashEffects", schema: "treasury");
            migrationBuilder.DropTable(name: "ReconciliationBatches", schema: "treasury");
            migrationBuilder.DropTable(name: "PaymentOrders", schema: "treasury");
            migrationBuilder.DropTable(name: "CashFlowForecasts", schema: "treasury");
            migrationBuilder.DropTable(name: "BankAccounts", schema: "treasury");
        }
    }
}

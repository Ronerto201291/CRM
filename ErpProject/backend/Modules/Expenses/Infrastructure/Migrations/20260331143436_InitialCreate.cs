using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Expenses.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "expenses");

            migrationBuilder.CreateTable(
                name: "AccountingEntries",
                schema: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AccountDebit = table.Column<string>(type: "text", nullable: false),
                    AccountCredit = table.Column<string>(type: "text", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    PostedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseDocumentLines",
                schema: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseDocumentLines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseDocuments",
                schema: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ExpenseUploadId = table.Column<Guid>(type: "uuid", nullable: true),
                    ExpenseUploadId1 = table.Column<Guid>(type: "uuid", nullable: true),
                    InvoiceNumber = table.Column<string>(type: "text", nullable: true),
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TaxBase = table.Column<decimal>(type: "numeric", nullable: true),
                    VATRate = table.Column<decimal>(type: "numeric", nullable: true),
                    VATAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    IRPFRate = table.Column<decimal>(type: "numeric", nullable: true),
                    IRPFAmount = table.Column<decimal>(type: "numeric", nullable: true),
                    Total = table.Column<decimal>(type: "numeric", nullable: true),
                    OcrRawData = table.Column<string>(type: "jsonb", nullable: true),
                    OcrConfidence = table.Column<decimal>(type: "numeric", nullable: true),
                    SupplierTaxId = table.Column<string>(type: "text", nullable: true),
                    SupplierName = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    IsValidated = table.Column<bool>(type: "boolean", nullable: false),
                    ValidatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                    HashSignature = table.Column<string>(type: "text", nullable: true),
                    SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                    AccountingEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ExpenseUploads",
                schema: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    PublicTokenUsed = table.Column<string>(type: "text", nullable: false),
                    FilePath = table.Column<string>(type: "text", nullable: false),
                    FileName = table.Column<string>(type: "text", nullable: false),
                    ContentType = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Comment = table.Column<string>(type: "text", nullable: true),
                    ExpenseDocumentId = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseUploads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseUploads_ExpenseDocuments_ExpenseDocumentId",
                        column: x => x.ExpenseDocumentId,
                        principalSchema: "expenses",
                        principalTable: "ExpenseDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AccountingEntries_ExpenseDocumentId",
                schema: "expenses",
                table: "AccountingEntries",
                column: "ExpenseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDocumentLines_DocumentId",
                schema: "expenses",
                table: "ExpenseDocumentLines",
                column: "ExpenseDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDocuments_CompanyId",
                schema: "expenses",
                table: "ExpenseDocuments",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseDocuments_ExpenseUploadId1",
                schema: "expenses",
                table: "ExpenseDocuments",
                column: "ExpenseUploadId1");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseUploads_CompanyId",
                schema: "expenses",
                table: "ExpenseUploads",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseUploads_ExpenseDocumentId",
                schema: "expenses",
                table: "ExpenseUploads",
                column: "ExpenseDocumentId");

            migrationBuilder.AddForeignKey(
                name: "FK_AccountingEntries_ExpenseDocuments_ExpenseDocumentId",
                schema: "expenses",
                table: "AccountingEntries",
                column: "ExpenseDocumentId",
                principalSchema: "expenses",
                principalTable: "ExpenseDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseDocumentLines_ExpenseDocuments_ExpenseDocumentId",
                schema: "expenses",
                table: "ExpenseDocumentLines",
                column: "ExpenseDocumentId",
                principalSchema: "expenses",
                principalTable: "ExpenseDocuments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ExpenseDocuments_ExpenseUploads_ExpenseUploadId1",
                schema: "expenses",
                table: "ExpenseDocuments",
                column: "ExpenseUploadId1",
                principalSchema: "expenses",
                principalTable: "ExpenseUploads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ExpenseUploads_ExpenseDocuments_ExpenseDocumentId",
                schema: "expenses",
                table: "ExpenseUploads");

            migrationBuilder.DropTable(
                name: "AccountingEntries",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "ExpenseDocumentLines",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "ExpenseDocuments",
                schema: "expenses");

            migrationBuilder.DropTable(
                name: "ExpenseUploads",
                schema: "expenses");
        }
    }
}

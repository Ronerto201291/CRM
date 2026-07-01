using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Billing.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFullQuoteModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Eliminar la tabla Quotes stub (solo tenía 4 columnas)
            migrationBuilder.DropTable(
                name: "Quotes",
                schema: "billing");

            // 2. Crear tabla Quotes completa
            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    // Numeración
                    Number = table.Column<string>(type: "text", nullable: false),
                    SeriesPrefix = table.Column<string>(type: "text", nullable: false, defaultValue: "PRE"),
                    FiscalYear = table.Column<int>(type: "integer", nullable: false),
                    SequenceNumber = table.Column<int>(type: "integer", nullable: false),
                    // Versionado
                    Version = table.Column<int>(type: "integer", nullable: false, defaultValue: 1),
                    ParentQuoteId = table.Column<Guid>(type: "uuid", nullable: true),
                    // Estado
                    Status = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                    // Cliente snapshot
                    ClientId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClientType = table.Column<string>(type: "text", nullable: false, defaultValue: "Manual"),
                    ClientName = table.Column<string>(type: "text", nullable: true),
                    ClientTaxId = table.Column<string>(type: "text", nullable: true),
                    ClientEmail = table.Column<string>(type: "text", nullable: true),
                    ClientPhone = table.Column<string>(type: "text", nullable: true),
                    ClientAddress = table.Column<string>(type: "text", nullable: true),
                    // Fechas
                    IssueDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ValidUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcceptedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RejectedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    // Financiero
                    Currency = table.Column<string>(type: "character(3)", maxLength: 3, nullable: false, defaultValue: "EUR"),
                    GlobalDiscountPct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    GlobalDiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    SubtotalBeforeDisc = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    SubtotalAfterDisc = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TaxBaseAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TaxBreakdown = table.Column<string>(type: "jsonb", nullable: false, defaultValueSql: "'[]'"),
                    // Notas
                    Notes = table.Column<string>(type: "text", nullable: true),
                    InternalNotes = table.Column<string>(type: "text", nullable: true),
                    // Portal
                    AcceptanceToken = table.Column<string>(type: "text", nullable: false),
                    // Conversión
                    ConvertedToInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    ConvertedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    // Auditoría extendida
                    CreatedById = table.Column<Guid>(type: "uuid", nullable: true),
                    LastModifiedById = table.Column<Guid>(type: "uuid", nullable: true),
                    // AuditableEntity timestamps
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Quotes_Quotes_ParentQuoteId",
                        column: x => x.ParentQuoteId,
                        principalSchema: "billing",
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            // 3. Tabla QuoteLines
            migrationBuilder.CreateTable(
                name: "QuoteLines",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: true),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ProductCode = table.Column<string>(type: "text", nullable: true),
                    Unit = table.Column<string>(type: "text", nullable: true),
                    Quantity = table.Column<decimal>(type: "numeric(18,4)", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(18,6)", nullable: false),
                    DiscountPct = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 0m),
                    DiscountAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false, defaultValue: 0m),
                    TaxRate = table.Column<decimal>(type: "numeric(5,2)", nullable: false, defaultValue: 21m),
                    LineSubtotal = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTaxBase = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTaxAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false),
                    LineTotalAmount = table.Column<decimal>(type: "numeric(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteLines_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "billing",
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 4. Tabla QuoteStatusHistory
            migrationBuilder.CreateTable(
                name: "QuoteStatusHistory",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    QuoteId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromStatus = table.Column<string>(type: "text", nullable: true),
                    ToStatus = table.Column<string>(type: "text", nullable: false),
                    ChangedById = table.Column<Guid>(type: "uuid", nullable: true),
                    ChangedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "text", nullable: true),
                    Metadata = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteStatusHistory", x => x.Id);
                    table.ForeignKey(
                        name: "FK_QuoteStatusHistory_Quotes_QuoteId",
                        column: x => x.QuoteId,
                        principalSchema: "billing",
                        principalTable: "Quotes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            // 5. Tabla QuoteNumberSeries
            migrationBuilder.CreateTable(
                name: "QuoteNumberSeries",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Prefix = table.Column<string>(type: "text", nullable: false, defaultValue: "PRE"),
                    LastNumber = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuoteNumberSeries", x => x.Id);
                });

            // 6. Índices
            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CompanyId",
                schema: "billing",
                table: "Quotes",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_CompanyId_Number",
                schema: "billing",
                table: "Quotes",
                columns: new[] { "CompanyId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_AcceptanceToken",
                schema: "billing",
                table: "Quotes",
                column: "AcceptanceToken",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Quotes_ParentQuoteId",
                schema: "billing",
                table: "Quotes",
                column: "ParentQuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteLines_QuoteId",
                schema: "billing",
                table: "QuoteLines",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteStatusHistory_QuoteId",
                schema: "billing",
                table: "QuoteStatusHistory",
                column: "QuoteId");

            migrationBuilder.CreateIndex(
                name: "IX_QuoteNumberSeries_CompanyId_Year_Prefix",
                schema: "billing",
                table: "QuoteNumberSeries",
                columns: new[] { "CompanyId", "Year", "Prefix" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "QuoteStatusHistory", schema: "billing");
            migrationBuilder.DropTable(name: "QuoteLines", schema: "billing");
            migrationBuilder.DropTable(name: "QuoteNumberSeries", schema: "billing");
            migrationBuilder.DropTable(name: "Quotes", schema: "billing");

            // Restaurar tabla stub original
            migrationBuilder.CreateTable(
                name: "Quotes",
                schema: "billing",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    ExpirationDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Quotes", x => x.Id);
                });
        }
    }
}

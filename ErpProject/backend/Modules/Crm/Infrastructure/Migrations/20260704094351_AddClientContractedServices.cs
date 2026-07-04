using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClientContractedServices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<bool>(
                name: "IsAcknowledged",
                schema: "crm",
                table: "ScheduledAlerts",
                type: "boolean",
                nullable: false,
                oldClrType: typeof(bool),
                oldType: "boolean",
                oldDefaultValue: false);

            migrationBuilder.CreateTable(
                name: "ServiceCatalogItems",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    DefaultPrice = table.Column<decimal>(type: "numeric", nullable: false),
                    DefaultTaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    DefaultPeriodicity = table.Column<string>(type: "text", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServiceCatalogItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClientContractedServices",
                schema: "crm",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceCatalogItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServiceName = table.Column<string>(type: "text", nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    TaxRate = table.Column<decimal>(type: "numeric", nullable: false),
                    Periodicity = table.Column<string>(type: "text", nullable: false),
                    StartDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    NextBillingDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<string>(type: "text", nullable: false),
                    LastInvoiceId = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClientContractedServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClientContractedServices_Clients_ClientId",
                        column: x => x.ClientId,
                        principalSchema: "crm",
                        principalTable: "Clients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ClientContractedServices_ServiceCatalogItems_ServiceCatalog~",
                        column: x => x.ServiceCatalogItemId,
                        principalSchema: "crm",
                        principalTable: "ServiceCatalogItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClientContractedServices_ClientId",
                schema: "crm",
                table: "ClientContractedServices",
                column: "ClientId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContractedServices_CompanyId",
                schema: "crm",
                table: "ClientContractedServices",
                column: "CompanyId");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContractedServices_NextBillingDate",
                schema: "crm",
                table: "ClientContractedServices",
                column: "NextBillingDate");

            migrationBuilder.CreateIndex(
                name: "IX_ClientContractedServices_ServiceCatalogItemId",
                schema: "crm",
                table: "ClientContractedServices",
                column: "ServiceCatalogItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ServiceCatalogItems_CompanyId",
                schema: "crm",
                table: "ServiceCatalogItems",
                column: "CompanyId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClientContractedServices",
                schema: "crm");

            migrationBuilder.DropTable(
                name: "ServiceCatalogItems",
                schema: "crm");

            migrationBuilder.AlterColumn<bool>(
                name: "IsAcknowledged",
                schema: "crm",
                table: "ScheduledAlerts",
                type: "boolean",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "boolean");
        }
    }
}

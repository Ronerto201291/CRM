using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Payroll.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialPayroll : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "payroll");

            migrationBuilder.CreateTable(
                name: "Employees",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    TaxId = table.Column<string>(type: "text", nullable: false),
                    FullName = table.Column<string>(type: "text", nullable: false),
                    SocialSecurityNumber = table.Column<string>(type: "text", nullable: true),
                    HireDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ContractType = table.Column<string>(type: "text", nullable: false),
                    WeeklyHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Employees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollSettlements",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollSettlements", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayrollLines",
                schema: "payroll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PayrollSettlementId = table.Column<Guid>(type: "uuid", nullable: false),
                    EmployeeId = table.Column<Guid>(type: "uuid", nullable: false),
                    GrossSalary = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CommonContingenciesBase = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EmployeeSocialSecurity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    EmployerSocialSecurity = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IrpfBase = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    IrpfRate = table.Column<decimal>(type: "numeric(8,4)", precision: 8, scale: 4, nullable: false),
                    IrpfWithheld = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    NetPay = table.Column<decimal>(type: "numeric(18,4)", precision: 18, scale: 4, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayrollLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayrollLines_Employees_EmployeeId",
                        column: x => x.EmployeeId,
                        principalSchema: "payroll",
                        principalTable: "Employees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PayrollLines_PayrollSettlements_PayrollSettlementId",
                        column: x => x.PayrollSettlementId,
                        principalSchema: "payroll",
                        principalTable: "PayrollSettlements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Employees_CompanyId_TaxId",
                schema: "payroll",
                table: "Employees",
                columns: new[] { "CompanyId", "TaxId" });

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_EmployeeId",
                schema: "payroll",
                table: "PayrollLines",
                column: "EmployeeId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollLines_PayrollSettlementId",
                schema: "payroll",
                table: "PayrollLines",
                column: "PayrollSettlementId");

            migrationBuilder.CreateIndex(
                name: "IX_PayrollSettlements_CompanyId_Year_Month",
                schema: "payroll",
                table: "PayrollSettlements",
                columns: new[] { "CompanyId", "Year", "Month" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PayrollLines",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "Employees",
                schema: "payroll");

            migrationBuilder.DropTable(
                name: "PayrollSettlements",
                schema: "payroll");
        }
    }
}

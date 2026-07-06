using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SeedTreasuryPayrollPurchasingSalesModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Treasury/Payroll/Purchasing/Sales are real code modules (own Api/Application/
            // Domain/Infrastructure projects, wired in Program.cs) but never got a PlanModule
            // seed row, unlike the other 7 modules seeded in AddLicensingOutboxAndConstraints.
            // Adding [RequiredModule("Treasury"/...)] to their controllers without this would
            // permanently 403 every tenant, since ModuleAuthorizationHandler fails closed when
            // the plan has no matching PlanModule row (ADR-0018 #42c).
            migrationBuilder.Sql(@"
                INSERT INTO ""PlanModules"" (""Id"", ""PlanId"", ""ModuleName"", ""IsIncluded"")
                SELECT gen_random_uuid(), p.""Id"", m.""ModuleName"", m.""IsIncluded""
                FROM ""Plans"" p
                CROSS JOIN (
                    SELECT 'Free' as plan, 'Treasury' as ""ModuleName"", false as ""IsIncluded""
                    UNION ALL SELECT 'Free', 'Payroll', false
                    UNION ALL SELECT 'Free', 'Purchasing', false
                    UNION ALL SELECT 'Free', 'Sales', false
                    UNION ALL SELECT 'Starter', 'Treasury', false
                    UNION ALL SELECT 'Starter', 'Payroll', false
                    UNION ALL SELECT 'Starter', 'Purchasing', false
                    UNION ALL SELECT 'Starter', 'Sales', false
                    UNION ALL SELECT 'Professional', 'Treasury', false
                    UNION ALL SELECT 'Professional', 'Payroll', true
                    UNION ALL SELECT 'Professional', 'Purchasing', true
                    UNION ALL SELECT 'Professional', 'Sales', true
                    UNION ALL SELECT 'Enterprise', 'Treasury', true
                    UNION ALL SELECT 'Enterprise', 'Payroll', true
                    UNION ALL SELECT 'Enterprise', 'Purchasing', true
                    UNION ALL SELECT 'Enterprise', 'Sales', true
                ) m
                WHERE p.""Name"" = m.plan
                  AND NOT EXISTS (
                      SELECT 1 FROM ""PlanModules"" pm
                      WHERE pm.""PlanId"" = p.""Id"" AND pm.""ModuleName"" = m.""ModuleName""
                  );
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                DELETE FROM ""PlanModules""
                WHERE ""ModuleName"" IN ('Treasury', 'Payroll', 'Purchasing', 'Sales');
            ");
        }
    }
}

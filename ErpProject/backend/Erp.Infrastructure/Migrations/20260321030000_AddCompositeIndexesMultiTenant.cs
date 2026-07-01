using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <inheritdoc />
public partial class AddCompositeIndexesMultiTenant : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Invoices: filtered lists by status and date-range reports
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CompanyId_Status",
            table: "Invoices",
            columns: new[] { "CompanyId", "Status" });

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CompanyId_IssueDate",
            table: "Invoices",
            columns: new[] { "CompanyId", "IssueDate" });

        // JournalEntries: accounting date-range queries
        migrationBuilder.CreateIndex(
            name: "IX_JournalEntries_CompanyId_Date",
            table: "JournalEntries",
            columns: new[] { "CompanyId", "Date" });

        // AuditLogs: audit history ordered by timestamp
        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CompanyId_Timestamp",
            table: "AuditLogs",
            columns: new[] { "CompanyId", "Timestamp" });

        // StockMovements: movement history by company + date
        migrationBuilder.CreateIndex(
            name: "IX_StockMovements_CompanyId_CreatedAt",
            table: "StockMovements",
            columns: new[] { "CompanyId", "CreatedAt" });

        // Leads: CRM funnel filter by status
        migrationBuilder.CreateIndex(
            name: "IX_Leads_CompanyId_Status",
            table: "Leads",
            columns: new[] { "CompanyId", "Status" });

        // Clients: alphabetical listing
        migrationBuilder.CreateIndex(
            name: "IX_Clients_CompanyId_Name",
            table: "Clients",
            columns: new[] { "CompanyId", "Name" });
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Invoices_CompanyId_Status",        table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_CompanyId_IssueDate",     table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_JournalEntries_CompanyId_Date",    table: "JournalEntries");
        migrationBuilder.DropIndex(name: "IX_AuditLogs_CompanyId_Timestamp",    table: "AuditLogs");
        migrationBuilder.DropIndex(name: "IX_StockMovements_CompanyId_CreatedAt", table: "StockMovements");
        migrationBuilder.DropIndex(name: "IX_Leads_CompanyId_Status",           table: "Leads");
        migrationBuilder.DropIndex(name: "IX_Clients_CompanyId_Name",           table: "Clients");
    }
}

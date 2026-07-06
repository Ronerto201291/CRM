using Erp.Modules.Crm.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Modules.Crm.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(CrmDbContext))]
    [Migration("20260402020000_AddLeadProspectFields")]
    public partial class AddLeadProspectFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TaxId",
                schema: "crm",
                table: "Leads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "Address",
                schema: "crm",
                table: "Leads",
                type: "text",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "ConvertedToClientId",
                schema: "crm",
                table: "Leads",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ConvertedToClientId", schema: "crm", table: "Leads");
            migrationBuilder.DropColumn(name: "Address",             schema: "crm", table: "Leads");
            migrationBuilder.DropColumn(name: "TaxId",               schema: "crm", table: "Leads");
        }
    }
}

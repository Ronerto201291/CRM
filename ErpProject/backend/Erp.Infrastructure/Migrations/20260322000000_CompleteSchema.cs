using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Erp.Infrastructure.Migrations;

/// <summary>
/// Comprehensive catch-up migration for a clean DB install.
///
/// Creates tables that were never added to any migration:
///   Suppliers, Contacts, ActivityLogs (CRM), ExpenseDocuments, ExpenseUploads,
///   ExpenseDocumentLines, AccountingEntries, RefreshTokens, UserPermissions.
///
/// Adds missing columns to existing tables:
///   Companies  → PublicUploadToken, QrUploadEnabled, StripeCustomerId
///   Users      → TotpSecret, TotpBackupCodes, RoleId
///   Permissions → Resource, Action  (ABAC)
///   RolePermissions → RoleName  (ABAC)
///   Leads      → Name, Email, Phone, CreatedAt, UpdatedAt
///   JournalEntries  → Description, SourceType, SourceId, IsPosted, PostedAt
///   JournalEntryLines → AccountCode, AccountName
///   Invoices   → FiscalYear, SequenceNumber, InvoiceType, RectifiedInvoiceId,
///                IrpfRate, IrpfAmount, SurchargeAmount, LockedAt, Hash,
///                PreviousHash, JournalEntryId
///   InvoiceLines → TaxAmount, SurchargeRate, SurchargeAmount
///   Subscriptions → StripeSubscriptionId, StripeStatus
///
/// Adds missing indexes: IX_Invoices_CompanyId, UQ_Invoices_CompanyId_Number,
///   IX_Clients_CompanyId, IX_AuditLogs_CompanyId, etc.
///
/// Adds missing DB triggers:
///   trg_no_delete_invoice   → Invoices      (Ley 11/2021 Antifraude)
///   trg_no_delete_journal   → JournalEntries (Ley 11/2021 Antifraude)
/// </summary>
public partial class CompleteSchema : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // ══════════════════════════════════════════════════════════════════════
        // SECTION 1 — MISSING COLUMNS ON EXISTING TABLES
        // ══════════════════════════════════════════════════════════════════════

        // ── Companies ────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "PublicUploadToken",
            table: "Companies",
            type: "text",
            nullable: false,
            defaultValueSql: "gen_random_uuid()::text");

        migrationBuilder.AddColumn<bool>(
            name: "QrUploadEnabled",
            table: "Companies",
            type: "boolean",
            nullable: false,
            defaultValue: true);

        migrationBuilder.AddColumn<string>(
            name: "StripeCustomerId",
            table: "Companies",
            type: "text",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Companies_PublicUploadToken",
            table: "Companies",
            column: "PublicUploadToken",
            unique: true);

        // ── Users ────────────────────────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "TotpSecret",
            table: "Users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "TotpBackupCodes",
            table: "Users",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "RoleId",
            table: "Users",
            type: "uuid",
            nullable: true);

        migrationBuilder.CreateIndex(
            name: "IX_Users_RoleId",
            table: "Users",
            column: "RoleId");

        migrationBuilder.AddForeignKey(
            name: "FK_Users_Roles_RoleId",
            table: "Users",
            column: "RoleId",
            principalTable: "Roles",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // ── Permissions — ABAC fields ─────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "Resource",
            table: "Permissions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Action",
            table: "Permissions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "UQ_Permissions_Resource_Action",
            table: "Permissions",
            columns: new[] { "Resource", "Action" },
            unique: true);

        // ── RolePermissions — ABAC string role name ───────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "RoleName",
            table: "RolePermissions",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.CreateIndex(
            name: "IX_RolePermissions_RoleName",
            table: "RolePermissions",
            column: "RoleName");

        // ── Leads — AuditableEntity fields + contact info ─────────────────────
        migrationBuilder.AddColumn<string>(
            name: "Name",
            table: "Leads",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Leads",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "Phone",
            table: "Leads",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<DateTime>(
            name: "CreatedAt",
            table: "Leads",
            type: "timestamp with time zone",
            nullable: false,
            defaultValueSql: "now()");

        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "Leads",
            type: "timestamp with time zone",
            nullable: true);

        // ── JournalEntries ────────────────────────────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "Description",
            table: "JournalEntries",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "SourceType",
            table: "JournalEntries",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "SourceId",
            table: "JournalEntries",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsPosted",
            table: "JournalEntries",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "PostedAt",
            table: "JournalEntries",
            type: "timestamp with time zone",
            nullable: true);

        // ── JournalEntryLines — PGC display fields ────────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "AccountCode",
            table: "JournalEntryLines",
            type: "text",
            nullable: false,
            defaultValue: "");

        migrationBuilder.AddColumn<string>(
            name: "AccountName",
            table: "JournalEntryLines",
            type: "text",
            nullable: false,
            defaultValue: "");

        // ── Invoices — many fiscal fields missing ─────────────────────────────
        migrationBuilder.AddColumn<int>(
            name: "FiscalYear",
            table: "Invoices",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<int>(
            name: "SequenceNumber",
            table: "Invoices",
            type: "integer",
            nullable: false,
            defaultValue: 0);

        migrationBuilder.AddColumn<string>(
            name: "InvoiceType",
            table: "Invoices",
            type: "text",
            nullable: false,
            defaultValue: "Normal");

        migrationBuilder.AddColumn<Guid>(
            name: "RectifiedInvoiceId",
            table: "Invoices",
            type: "uuid",
            nullable: true);

        migrationBuilder.AddColumn<decimal>(
            name: "IrpfRate",
            table: "Invoices",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "IrpfAmount",
            table: "Invoices",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "SurchargeAmount",
            table: "Invoices",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<DateTime>(
            name: "LockedAt",
            table: "Invoices",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "Hash",
            table: "Invoices",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "PreviousHash",
            table: "Invoices",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<Guid>(
            name: "JournalEntryId",
            table: "Invoices",
            type: "uuid",
            nullable: true);

        // Indexes on Invoices (defined in DbContext.OnModelCreating but never in a migration)
        migrationBuilder.CreateIndex(
            name: "IX_Invoices_CompanyId",
            table: "Invoices",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "UQ_Invoices_CompanyId_Number",
            table: "Invoices",
            columns: new[] { "CompanyId", "Number" },
            unique: true);

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_RectifiedInvoiceId",
            table: "Invoices",
            column: "RectifiedInvoiceId");

        migrationBuilder.CreateIndex(
            name: "IX_Invoices_JournalEntryId",
            table: "Invoices",
            column: "JournalEntryId");

        migrationBuilder.AddForeignKey(
            name: "FK_Invoices_Invoices_RectifiedInvoiceId",
            table: "Invoices",
            column: "RectifiedInvoiceId",
            principalTable: "Invoices",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.AddForeignKey(
            name: "FK_Invoices_JournalEntries_JournalEntryId",
            table: "Invoices",
            column: "JournalEntryId",
            principalTable: "JournalEntries",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        // ── InvoiceLines — Spanish tax fields ────────────────────────────────
        migrationBuilder.AddColumn<decimal>(
            name: "TaxAmount",
            table: "InvoiceLines",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "SurchargeRate",
            table: "InvoiceLines",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        migrationBuilder.AddColumn<decimal>(
            name: "SurchargeAmount",
            table: "InvoiceLines",
            type: "numeric",
            nullable: false,
            defaultValue: 0m);

        // ── Subscriptions — Stripe integration fields ─────────────────────────
        migrationBuilder.AddColumn<string>(
            name: "StripeSubscriptionId",
            table: "Subscriptions",
            type: "text",
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "StripeStatus",
            table: "Subscriptions",
            type: "text",
            nullable: true,
            defaultValue: "inactive");

        // ── Missing single-column CompanyId indexes (in DbContext but not in any migration)
        migrationBuilder.CreateIndex(
            name: "IX_Clients_CompanyId",
            table: "Clients",
            column: "CompanyId");

        migrationBuilder.CreateIndex(
            name: "IX_AuditLogs_CompanyId",
            table: "AuditLogs",
            column: "CompanyId");

        // ══════════════════════════════════════════════════════════════════════
        // SECTION 2 — CREATE MISSING TABLES
        // Order: parents before children; circular ExpenseDocuments↔ExpenseUploads
        // is resolved by creating both then adding the cross-FK with AddForeignKey.
        // ══════════════════════════════════════════════════════════════════════

        // ── Suppliers ─────────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "Suppliers",
            columns: table => new
            {
                Id          = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId   = table.Column<Guid>(type: "uuid", nullable: false),
                Name        = table.Column<string>(type: "text", nullable: false),
                TaxId       = table.Column<string>(type: "text", nullable: false),
                Email       = table.Column<string>(type: "text", nullable: false),
                Phone       = table.Column<string>(type: "text", nullable: false),
                Address     = table.Column<string>(type: "text", nullable: false),
                BankAccount = table.Column<string>(type: "text", nullable: true),
                IsActive    = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                CreatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt   = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Suppliers", x => x.Id);
            });

        migrationBuilder.CreateIndex(
            name: "IX_Suppliers_CompanyId",
            table: "Suppliers",
            column: "CompanyId");

        // ── Contacts (links to Clients and/or Suppliers) ──────────────────────
        migrationBuilder.CreateTable(
            name: "Contacts",
            columns: table => new
            {
                Id         = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId  = table.Column<Guid>(type: "uuid", nullable: false),
                Name       = table.Column<string>(type: "text", nullable: false),
                Email      = table.Column<string>(type: "text", nullable: false),
                Phone      = table.Column<string>(type: "text", nullable: false),
                Position   = table.Column<string>(type: "text", nullable: false),
                ClientId   = table.Column<Guid>(type: "uuid", nullable: true),
                SupplierId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt  = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_Contacts", x => x.Id);
                table.ForeignKey(
                    name: "FK_Contacts_Clients_ClientId",
                    column: x => x.ClientId,
                    principalTable: "Clients",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
                table.ForeignKey(
                    name: "FK_Contacts_Suppliers_SupplierId",
                    column: x => x.SupplierId,
                    principalTable: "Suppliers",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(name: "IX_Contacts_CompanyId",  table: "Contacts", column: "CompanyId");
        migrationBuilder.CreateIndex(name: "IX_Contacts_ClientId",   table: "Contacts", column: "ClientId");
        migrationBuilder.CreateIndex(name: "IX_Contacts_SupplierId", table: "Contacts", column: "SupplierId");

        // ── ActivityLogs (CRM timeline) ────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ActivityLogs",
            columns: table => new
            {
                Id         = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId  = table.Column<Guid>(type: "uuid", nullable: false),
                UserId     = table.Column<Guid>(type: "uuid", nullable: true),
                EntityType = table.Column<string>(type: "text", nullable: false),
                EntityId   = table.Column<Guid>(type: "uuid", nullable: false),
                Action     = table.Column<string>(type: "text", nullable: false),
                Description = table.Column<string>(type: "text", nullable: true),
                Timestamp  = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ActivityLogs", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_ActivityLogs_CompanyId", table: "ActivityLogs", column: "CompanyId");

        // ── ExpenseDocuments (circular ref: created WITHOUT FK to ExpenseUploads) ──
        migrationBuilder.CreateTable(
            name: "ExpenseDocuments",
            columns: table => new
            {
                Id               = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId        = table.Column<Guid>(type: "uuid", nullable: false),
                ExpenseUploadId  = table.Column<Guid>(type: "uuid", nullable: true),   // FK added after ExpenseUploads is created
                InvoiceNumber    = table.Column<string>(type: "text", nullable: true),
                IssueDate        = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                TaxBase          = table.Column<decimal>(type: "numeric", nullable: true),
                VATRate          = table.Column<decimal>(type: "numeric", nullable: true),
                VATAmount        = table.Column<decimal>(type: "numeric", nullable: true),
                IRPFRate         = table.Column<decimal>(type: "numeric", nullable: true),
                IRPFAmount       = table.Column<decimal>(type: "numeric", nullable: true),
                Total            = table.Column<decimal>(type: "numeric", nullable: true),
                OcrRawData       = table.Column<string>(type: "jsonb", nullable: true),
                OcrConfidence    = table.Column<decimal>(type: "numeric(5,1)", nullable: true),
                SupplierTaxId    = table.Column<string>(type: "text", nullable: true),
                SupplierName     = table.Column<string>(type: "text", nullable: true),
                Status           = table.Column<string>(type: "text", nullable: false, defaultValue: "Draft"),
                IsValidated      = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ValidatedAt      = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                IsLocked         = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                HashSignature    = table.Column<string>(type: "text", nullable: true),
                SupplierId       = table.Column<Guid>(type: "uuid", nullable: true),
                AccountingEntryId = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseDocuments", x => x.Id);
            });

        migrationBuilder.CreateIndex(name: "IX_ExpenseDocuments_CompanyId", table: "ExpenseDocuments", column: "CompanyId");

        // ── ExpenseUploads (FK → ExpenseDocuments) ────────────────────────────
        migrationBuilder.CreateTable(
            name: "ExpenseUploads",
            columns: table => new
            {
                Id                 = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId          = table.Column<Guid>(type: "uuid", nullable: false),
                PublicTokenUsed    = table.Column<string>(type: "text", nullable: false),
                FilePath           = table.Column<string>(type: "text", nullable: false),
                FileName           = table.Column<string>(type: "text", nullable: false),
                ContentType        = table.Column<string>(type: "text", nullable: false),
                Status             = table.Column<string>(type: "text", nullable: false, defaultValue: "Pending"),
                UploadedAt         = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Comment            = table.Column<string>(type: "text", nullable: true),
                ExpenseDocumentId  = table.Column<Guid>(type: "uuid", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseUploads", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExpenseUploads_ExpenseDocuments_ExpenseDocumentId",
                    column: x => x.ExpenseDocumentId,
                    principalTable: "ExpenseDocuments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.SetNull);
            });

        migrationBuilder.CreateIndex(name: "IX_ExpenseUploads_CompanyId",         table: "ExpenseUploads", column: "CompanyId");
        migrationBuilder.CreateIndex(name: "IX_ExpenseUploads_ExpenseDocumentId", table: "ExpenseUploads", column: "ExpenseDocumentId");

        // Close the circular reference: ExpenseDocuments → ExpenseUploads
        migrationBuilder.AddForeignKey(
            name: "FK_ExpenseDocuments_ExpenseUploads_ExpenseUploadId",
            table: "ExpenseDocuments",
            column: "ExpenseUploadId",
            principalTable: "ExpenseUploads",
            principalColumn: "Id",
            onDelete: ReferentialAction.SetNull);

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseDocuments_ExpenseUploadId",
            table: "ExpenseDocuments",
            column: "ExpenseUploadId");

        // ── ExpenseDocumentLines ───────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "ExpenseDocumentLines",
            columns: table => new
            {
                Id                = table.Column<Guid>(type: "uuid", nullable: false),
                ExpenseDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                Description       = table.Column<string>(type: "text", nullable: true),
                Quantity          = table.Column<decimal>(type: "numeric", nullable: false, defaultValue: 1m),
                UnitPrice         = table.Column<decimal>(type: "numeric", nullable: false),
                VATRate           = table.Column<decimal>(type: "numeric", nullable: true),
                LineTotal         = table.Column<decimal>(type: "numeric", nullable: false),
                ProductId         = table.Column<Guid>(type: "uuid", nullable: true),
                SortOrder         = table.Column<int>(type: "integer", nullable: false, defaultValue: 0)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_ExpenseDocumentLines", x => x.Id);
                table.ForeignKey(
                    name: "FK_ExpenseDocumentLines_ExpenseDocuments_ExpenseDocumentId",
                    column: x => x.ExpenseDocumentId,
                    principalTable: "ExpenseDocuments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "IX_ExpenseDocumentLines_DocumentId",
            table: "ExpenseDocumentLines",
            column: "ExpenseDocumentId");

        // ── AccountingEntries (auto-generated on expense approval) ─────────────
        migrationBuilder.CreateTable(
            name: "AccountingEntries",
            columns: table => new
            {
                Id                = table.Column<Guid>(type: "uuid", nullable: false),
                CompanyId         = table.Column<Guid>(type: "uuid", nullable: false),
                ExpenseDocumentId = table.Column<Guid>(type: "uuid", nullable: false),
                AccountDebit      = table.Column<string>(type: "text", nullable: false),
                AccountCredit     = table.Column<string>(type: "text", nullable: false),
                Amount            = table.Column<decimal>(type: "numeric", nullable: false),
                PostedAt          = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_AccountingEntries", x => x.Id);
                table.ForeignKey(
                    name: "FK_AccountingEntries_ExpenseDocuments_ExpenseDocumentId",
                    column: x => x.ExpenseDocumentId,
                    principalTable: "ExpenseDocuments",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_AccountingEntries_CompanyId",         table: "AccountingEntries", column: "CompanyId");
        migrationBuilder.CreateIndex(name: "IX_AccountingEntries_ExpenseDocumentId", table: "AccountingEntries", column: "ExpenseDocumentId");

        // ── RefreshTokens ─────────────────────────────────────────────────────
        migrationBuilder.CreateTable(
            name: "RefreshTokens",
            columns: table => new
            {
                Id               = table.Column<Guid>(type: "uuid", nullable: false),
                UserId           = table.Column<Guid>(type: "uuid", nullable: false),
                Token            = table.Column<string>(type: "text", nullable: false),
                ExpiresAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CreatedAt        = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                IsRevoked        = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                ReplacedByToken  = table.Column<string>(type: "text", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_RefreshTokens", x => x.Id);
                table.ForeignKey(
                    name: "FK_RefreshTokens_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(name: "IX_RefreshTokens_UserId", table: "RefreshTokens", column: "UserId");
        migrationBuilder.CreateIndex(name: "UQ_RefreshTokens_Token",  table: "RefreshTokens", column: "Token", unique: true);

        // ── UserPermissions (ABAC per-user overrides) ─────────────────────────
        migrationBuilder.CreateTable(
            name: "UserPermissions",
            columns: table => new
            {
                Id           = table.Column<Guid>(type: "uuid", nullable: false),
                UserId       = table.Column<Guid>(type: "uuid", nullable: false),
                PermissionId = table.Column<Guid>(type: "uuid", nullable: false),
                IsGranted    = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                GrantedBy    = table.Column<string>(type: "text", nullable: true),
                ExpiresAt    = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserPermissions", x => x.Id);
                table.ForeignKey(
                    name: "FK_UserPermissions_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
                table.ForeignKey(
                    name: "FK_UserPermissions_Permissions_PermissionId",
                    column: x => x.PermissionId,
                    principalTable: "Permissions",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.CreateIndex(
            name: "UQ_UserPermissions_User_Permission",
            table: "UserPermissions",
            columns: new[] { "UserId", "PermissionId" },
            unique: true);
        migrationBuilder.CreateIndex(name: "IX_UserPermissions_UserId",       table: "UserPermissions", column: "UserId");
        migrationBuilder.CreateIndex(name: "IX_UserPermissions_PermissionId", table: "UserPermissions", column: "PermissionId");

        // ══════════════════════════════════════════════════════════════════════
        // SECTION 3 — MISSING DB TRIGGERS (Ley 11/2021 Antifraude)
        // ══════════════════════════════════════════════════════════════════════

        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION prevent_invoice_delete()
            RETURNS TRIGGER AS $$
            BEGIN
                RAISE EXCEPTION 'DELETE on Invoices is forbidden by Ley 11/2021 Antifraude';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER trg_no_delete_invoice
            BEFORE DELETE ON public.""Invoices""
            FOR EACH ROW EXECUTE FUNCTION prevent_invoice_delete();
        ");

        migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION prevent_journalentry_delete()
            RETURNS TRIGGER AS $$
            BEGIN
                RAISE EXCEPTION 'DELETE on JournalEntries is forbidden by Ley 11/2021 Antifraude';
            END;
            $$ LANGUAGE plpgsql;

            CREATE TRIGGER trg_no_delete_journal
            BEFORE DELETE ON public.""JournalEntries""
            FOR EACH ROW EXECUTE FUNCTION prevent_journalentry_delete();
        ");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // ── Triggers ──────────────────────────────────────────────────────────
        migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_no_delete_invoice ON public.""Invoices"";");
        migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_no_delete_journal ON public.""JournalEntries"";");
        migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS prevent_invoice_delete();");
        migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS prevent_journalentry_delete();");

        // ── New tables (reverse dependency order) ─────────────────────────────
        migrationBuilder.DropTable(name: "UserPermissions");
        migrationBuilder.DropTable(name: "RefreshTokens");
        migrationBuilder.DropTable(name: "AccountingEntries");
        migrationBuilder.DropTable(name: "ExpenseDocumentLines");

        // Break circular ref before dropping
        migrationBuilder.DropForeignKey(name: "FK_ExpenseDocuments_ExpenseUploads_ExpenseUploadId", table: "ExpenseDocuments");
        migrationBuilder.DropIndex(name: "IX_ExpenseDocuments_ExpenseUploadId", table: "ExpenseDocuments");

        migrationBuilder.DropTable(name: "ExpenseUploads");
        migrationBuilder.DropTable(name: "ExpenseDocuments");
        migrationBuilder.DropTable(name: "ActivityLogs");
        migrationBuilder.DropTable(name: "Contacts");
        migrationBuilder.DropTable(name: "Suppliers");

        // ── Undo column additions ─────────────────────────────────────────────
        migrationBuilder.DropIndex(name: "IX_AuditLogs_CompanyId",     table: "AuditLogs");
        migrationBuilder.DropIndex(name: "IX_Clients_CompanyId",        table: "Clients");

        migrationBuilder.DropColumn(name: "StripeStatus",          table: "Subscriptions");
        migrationBuilder.DropColumn(name: "StripeSubscriptionId",   table: "Subscriptions");

        migrationBuilder.DropColumn(name: "SurchargeAmount", table: "InvoiceLines");
        migrationBuilder.DropColumn(name: "SurchargeRate",   table: "InvoiceLines");
        migrationBuilder.DropColumn(name: "TaxAmount",       table: "InvoiceLines");

        migrationBuilder.DropForeignKey(name: "FK_Invoices_JournalEntries_JournalEntryId",  table: "Invoices");
        migrationBuilder.DropForeignKey(name: "FK_Invoices_Invoices_RectifiedInvoiceId",    table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_JournalEntryId",       table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_RectifiedInvoiceId",   table: "Invoices");
        migrationBuilder.DropIndex(name: "UQ_Invoices_CompanyId_Number",     table: "Invoices");
        migrationBuilder.DropIndex(name: "IX_Invoices_CompanyId",            table: "Invoices");
        migrationBuilder.DropColumn(name: "JournalEntryId",    table: "Invoices");
        migrationBuilder.DropColumn(name: "PreviousHash",      table: "Invoices");
        migrationBuilder.DropColumn(name: "Hash",              table: "Invoices");
        migrationBuilder.DropColumn(name: "LockedAt",          table: "Invoices");
        migrationBuilder.DropColumn(name: "SurchargeAmount",   table: "Invoices");
        migrationBuilder.DropColumn(name: "IrpfAmount",        table: "Invoices");
        migrationBuilder.DropColumn(name: "IrpfRate",          table: "Invoices");
        migrationBuilder.DropColumn(name: "RectifiedInvoiceId",table: "Invoices");
        migrationBuilder.DropColumn(name: "InvoiceType",       table: "Invoices");
        migrationBuilder.DropColumn(name: "SequenceNumber",    table: "Invoices");
        migrationBuilder.DropColumn(name: "FiscalYear",        table: "Invoices");

        migrationBuilder.DropColumn(name: "AccountName", table: "JournalEntryLines");
        migrationBuilder.DropColumn(name: "AccountCode", table: "JournalEntryLines");

        migrationBuilder.DropColumn(name: "PostedAt",     table: "JournalEntries");
        migrationBuilder.DropColumn(name: "IsPosted",     table: "JournalEntries");
        migrationBuilder.DropColumn(name: "SourceId",     table: "JournalEntries");
        migrationBuilder.DropColumn(name: "SourceType",   table: "JournalEntries");
        migrationBuilder.DropColumn(name: "Description",  table: "JournalEntries");

        migrationBuilder.DropColumn(name: "UpdatedAt", table: "Leads");
        migrationBuilder.DropColumn(name: "CreatedAt", table: "Leads");
        migrationBuilder.DropColumn(name: "Phone",     table: "Leads");
        migrationBuilder.DropColumn(name: "Email",     table: "Leads");
        migrationBuilder.DropColumn(name: "Name",      table: "Leads");

        migrationBuilder.DropIndex(name: "IX_RolePermissions_RoleName", table: "RolePermissions");
        migrationBuilder.DropColumn(name: "RoleName", table: "RolePermissions");

        migrationBuilder.DropIndex(name: "UQ_Permissions_Resource_Action", table: "Permissions");
        migrationBuilder.DropColumn(name: "Action",   table: "Permissions");
        migrationBuilder.DropColumn(name: "Resource", table: "Permissions");

        migrationBuilder.DropForeignKey(name: "FK_Users_Roles_RoleId", table: "Users");
        migrationBuilder.DropIndex(name: "IX_Users_RoleId", table: "Users");
        migrationBuilder.DropColumn(name: "RoleId",          table: "Users");
        migrationBuilder.DropColumn(name: "TotpBackupCodes", table: "Users");
        migrationBuilder.DropColumn(name: "TotpSecret",      table: "Users");

        migrationBuilder.DropIndex(name: "IX_Companies_PublicUploadToken", table: "Companies");
        migrationBuilder.DropColumn(name: "StripeCustomerId",  table: "Companies");
        migrationBuilder.DropColumn(name: "QrUploadEnabled",   table: "Companies");
        migrationBuilder.DropColumn(name: "PublicUploadToken", table: "Companies");
    }
}

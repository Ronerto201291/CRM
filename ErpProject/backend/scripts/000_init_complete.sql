-- ============================================================
-- ERP SaaS - MASTER INSTALL SCRIPT
-- Version: 1.0.0 (2026-03-21)
-- Run order: este archivo, luego 002_abac_rabbitmq.sql
-- Idempotente: usa IF NOT EXISTS / CREATE OR REPLACE en todo
-- Compatible con: PostgreSQL 14+
-- ============================================================
-- Ejecutar como superuser o rol con permisos de CREATE TABLE.
-- ============================================================

-- ============================================================
-- EXTENSIONES
-- ============================================================
CREATE EXTENSION IF NOT EXISTS "pgcrypto";   -- gen_random_uuid()
CREATE EXTENSION IF NOT EXISTS "unaccent";   -- busquedas sin tildes

-- ============================================================
-- TABLAS BASE (multi-tenant)
-- ============================================================

CREATE TABLE IF NOT EXISTS "Companies" (
    "Id"                   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"                 TEXT        NOT NULL,
    "TaxId"                TEXT        NOT NULL DEFAULT '',
    "Address"              TEXT,
    "Phone"                TEXT,
    "Email"                TEXT,
    "StripeCustomerId"     TEXT,
    "PublicUploadToken"    TEXT        NOT NULL DEFAULT gen_random_uuid()::text,
    "QrUploadEnabled"      BOOLEAN     NOT NULL DEFAULT true,
    "CreatedAt"            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Companies_TaxId" ON "Companies"("TaxId");

CREATE TABLE IF NOT EXISTS "Plans" (
    "Id"                   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name"                 TEXT        NOT NULL,
    "Description"          TEXT        NOT NULL DEFAULT '',
    "MonthlyPrice"         NUMERIC     NOT NULL DEFAULT 0,
    "YearlyPrice"          NUMERIC     NOT NULL DEFAULT 0,
    "MaxUsers"             INT         NOT NULL DEFAULT 1,
    "MaxInvoicesPerMonth"  INT         NOT NULL DEFAULT 50,
    "IsActive"             BOOLEAN     NOT NULL DEFAULT true,
    "SortOrder"            INT         NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS "PlanModules" (
    "Id"         UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "PlanId"     UUID    NOT NULL REFERENCES "Plans"("Id") ON DELETE CASCADE,
    "ModuleName" TEXT    NOT NULL,
    "IsIncluded" BOOLEAN NOT NULL DEFAULT true
);

CREATE TABLE IF NOT EXISTS "Subscriptions" (
    "Id"                   UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"            UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "PlanId"               UUID        REFERENCES "Plans"("Id"),
    "Status"               TEXT        NOT NULL DEFAULT 'active',
    "StripeSubscriptionId" TEXT,
    "StripeStatus"         TEXT        NOT NULL DEFAULT 'inactive',
    "CurrentPeriodStart"   TIMESTAMPTZ,
    "CurrentPeriodEnd"     TIMESTAMPTZ,
    "CreatedAt"            TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"            TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Subscriptions_CompanyId" ON "Subscriptions"("CompanyId");

CREATE TABLE IF NOT EXISTS "TenantModules" (
    "Id"          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID    NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "ModuleName"  TEXT    NOT NULL,
    "IsEnabled"   BOOLEAN NOT NULL DEFAULT true,
    CONSTRAINT "UQ_TenantModules_Company_Module" UNIQUE ("CompanyId", "ModuleName")
);
CREATE INDEX IF NOT EXISTS "IX_TenantModules_CompanyId" ON "TenantModules"("CompanyId");

-- ============================================================
-- USUARIOS Y AUTENTICACION
-- ============================================================

CREATE TABLE IF NOT EXISTS "Roles" (
    "Id"          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"        TEXT NOT NULL,
    "Description" TEXT NOT NULL DEFAULT '',
    CONSTRAINT "UQ_Roles_Company_Name" UNIQUE ("CompanyId", "Name")
);
CREATE INDEX IF NOT EXISTS "IX_Roles_CompanyId" ON "Roles"("CompanyId");

CREATE TABLE IF NOT EXISTS "Users" (
    "Id"               UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"        UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Email"            TEXT        NOT NULL,
    "PasswordHash"     TEXT        NOT NULL,
    "FullName"         TEXT        NOT NULL DEFAULT '',
    "Role"             TEXT        NOT NULL DEFAULT 'Viewer',
    "IsActive"         BOOLEAN     NOT NULL DEFAULT true,
    "TotpSecret"       TEXT,
    "TotpBackupCodes"  TEXT,
    "RefreshToken"     TEXT,
    "RefreshTokenExp"  TIMESTAMPTZ,
    "CreatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"        TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "UQ_Users_Company_Email" UNIQUE ("CompanyId", "Email")
);
CREATE INDEX IF NOT EXISTS "IX_Users_CompanyId"  ON "Users"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Users_Email"       ON "Users"("Email");

CREATE TABLE IF NOT EXISTS "Permissions" (
    "Id"          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Code"        TEXT NOT NULL UNIQUE,
    "Description" TEXT NOT NULL DEFAULT '',
    "Resource"    TEXT NOT NULL DEFAULT '',
    "Action"      TEXT NOT NULL DEFAULT '',
    CONSTRAINT "UQ_Permissions_Resource_Action" UNIQUE ("Resource", "Action")
);

CREATE TABLE IF NOT EXISTS "RolePermissions" (
    "RoleId"       UUID NOT NULL REFERENCES "Roles"("Id") ON DELETE CASCADE,
    "PermissionId" UUID NOT NULL REFERENCES "Permissions"("Id") ON DELETE CASCADE,
    "RoleName"     TEXT NOT NULL DEFAULT '',
    PRIMARY KEY ("RoleId", "PermissionId")
);
CREATE INDEX IF NOT EXISTS "IX_RolePermissions_RoleName"     ON "RolePermissions"("RoleName");
CREATE INDEX IF NOT EXISTS "IX_RolePermissions_PermissionId" ON "RolePermissions"("PermissionId");

CREATE TABLE IF NOT EXISTS "UserPermissions" (
    "Id"           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId"       UUID        NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "PermissionId" UUID        NOT NULL REFERENCES "Permissions"("Id") ON DELETE CASCADE,
    "IsGranted"    BOOLEAN     NOT NULL DEFAULT true,
    "GrantedBy"    TEXT,
    "ExpiresAt"    TIMESTAMPTZ,
    CONSTRAINT "UQ_UserPermissions_User_Permission" UNIQUE ("UserId", "PermissionId")
);
CREATE INDEX IF NOT EXISTS "IX_UserPermissions_UserId"       ON "UserPermissions"("UserId");
CREATE INDEX IF NOT EXISTS "IX_UserPermissions_PermissionId" ON "UserPermissions"("PermissionId");

-- ============================================================
-- CRM
-- ============================================================

CREATE TABLE IF NOT EXISTS "Clients" (
    "Id"        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId" UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"      TEXT        NOT NULL,
    "TaxId"     TEXT        NOT NULL DEFAULT '',
    "Email"     TEXT,
    "Phone"     TEXT,
    "Address"   TEXT,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Clients_CompanyId" ON "Clients"("CompanyId");

CREATE TABLE IF NOT EXISTS "Suppliers" (
    "Id"          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"        TEXT        NOT NULL,
    "TaxId"       TEXT        NOT NULL DEFAULT '',
    "Email"       TEXT,
    "Phone"       TEXT,
    "BankAccount" TEXT,
    "IsActive"    BOOLEAN     NOT NULL DEFAULT true,
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Suppliers_CompanyId" ON "Suppliers"("CompanyId");

CREATE TABLE IF NOT EXISTS "Contacts" (
    "Id"         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"  UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"       TEXT        NOT NULL,
    "Email"      TEXT,
    "Phone"      TEXT,
    "Position"   TEXT,
    "ClientId"   UUID        REFERENCES "Clients"("Id")   ON DELETE SET NULL,
    "SupplierId" UUID        REFERENCES "Suppliers"("Id") ON DELETE SET NULL,
    "CreatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Contacts_CompanyId"  ON "Contacts"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Contacts_ClientId"   ON "Contacts"("ClientId");
CREATE INDEX IF NOT EXISTS "IX_Contacts_SupplierId" ON "Contacts"("SupplierId");

CREATE TABLE IF NOT EXISTS "Leads" (
    "Id"        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId" UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"      TEXT        NOT NULL,
    "Email"     TEXT,
    "Phone"     TEXT,
    "Status"    TEXT        NOT NULL DEFAULT 'New',
    "Source"    TEXT,
    "Notes"     TEXT,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Leads_CompanyId" ON "Leads"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Leads_Status"    ON "Leads"("Status");

-- ============================================================
-- FACTURACION (Billing)
-- Cumplimiento: RD 1619/2012 + Ley 11/2021 Antifraude + RD 1007/2023 Verifactu
-- ============================================================

CREATE TABLE IF NOT EXISTS "Invoices" (
    "Id"                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"         UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE RESTRICT,
    "ClientId"          UUID        NOT NULL REFERENCES "Clients"("Id")   ON DELETE RESTRICT,
    -- Numeracion correlativa por serie + ejercicio fiscal (RD 1619/2012 art. 6)
    "Number"            TEXT        NOT NULL,   -- ej. "A-2026-000001"
    "Series"            TEXT        NOT NULL DEFAULT 'A',
    "FiscalYear"        INT         NOT NULL,
    "SequenceNumber"    INT         NOT NULL,
    -- Tipo factura
    "InvoiceType"       TEXT        NOT NULL DEFAULT 'Normal',  -- Normal | Rectificativa
    "RectifiedInvoiceId" UUID       REFERENCES "Invoices"("Id"),
    -- Fechas
    "IssueDate"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "DueDate"           TIMESTAMPTZ NOT NULL,
    -- Importes fiscales
    "Subtotal"          NUMERIC     NOT NULL DEFAULT 0,
    "TaxAmount"         NUMERIC     NOT NULL DEFAULT 0,
    "IrpfRate"          NUMERIC     NOT NULL DEFAULT 0,
    "IrpfAmount"        NUMERIC     NOT NULL DEFAULT 0,
    "SurchargeAmount"   NUMERIC     NOT NULL DEFAULT 0,
    "Total"             NUMERIC     NOT NULL DEFAULT 0,
    -- Estado
    "Status"            TEXT        NOT NULL DEFAULT 'Draft',  -- Draft | Issued | Paid | Locked
    "IsLocked"          BOOLEAN     NOT NULL DEFAULT false,
    "LockedAt"          TIMESTAMPTZ,
    -- Hash encadenado (Ley 11/2021 Antifraude)
    "Hash"              TEXT,           -- SHA256(Number + Total + PreviousHash)
    "PreviousHash"      TEXT,
    -- Verifactu (RD 1007/2023)
    "VerifactuHuella"   TEXT,           -- SHA256 segun Annex II
    "VerifactuQrUrl"    TEXT,           -- URL AEAT para QR en PDF
    -- Contabilidad
    "JournalEntryId"    UUID,
    "CreatedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    CONSTRAINT "UQ_Invoices_Company_Number" UNIQUE ("CompanyId", "Number")
);
CREATE INDEX IF NOT EXISTS "IX_Invoices_CompanyId"   ON "Invoices"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Invoices_ClientId"    ON "Invoices"("ClientId");
CREATE INDEX IF NOT EXISTS "IX_Invoices_Status"      ON "Invoices"("Status");
CREATE INDEX IF NOT EXISTS "IX_Invoices_IssueDate"   ON "Invoices"("IssueDate");
CREATE INDEX IF NOT EXISTS "IX_Invoices_FiscalYear"  ON "Invoices"("CompanyId", "Series", "FiscalYear");

CREATE TABLE IF NOT EXISTS "InvoiceLines" (
    "Id"             UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "InvoiceId"      UUID    NOT NULL REFERENCES "Invoices"("Id") ON DELETE CASCADE,
    "Description"    TEXT    NOT NULL,
    "Quantity"       NUMERIC NOT NULL DEFAULT 1,
    "UnitPrice"      NUMERIC NOT NULL DEFAULT 0,
    "TaxRate"        NUMERIC NOT NULL DEFAULT 21,
    "SurchargeRate"  NUMERIC NOT NULL DEFAULT 0,
    "LineTotal"      NUMERIC NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS "IX_InvoiceLines_InvoiceId" ON "InvoiceLines"("InvoiceId");

-- ============================================================
-- GASTOS Y OCR (Expenses)
-- Cumplimiento: RD 1619/2012 + RGPD + Ley 11/2021
-- ============================================================

CREATE TABLE IF NOT EXISTS "ExpenseUploads" (
    "Id"            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"     UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "FileName"      TEXT        NOT NULL,
    "ContentType"   TEXT        NOT NULL DEFAULT 'image/jpeg',
    "FilePath"      TEXT        NOT NULL,
    "FileSize"      BIGINT      NOT NULL DEFAULT 0,
    "UploadToken"   TEXT,
    "Status"        TEXT        NOT NULL DEFAULT 'Pending',  -- Pending | Processing | Done | Error
    "OcrProcessedAt" TIMESTAMPTZ,
    "OcrError"      TEXT,
    "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_ExpenseUploads_CompanyId" ON "ExpenseUploads"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_ExpenseUploads_Status"    ON "ExpenseUploads"("Status");

CREATE TABLE IF NOT EXISTS "ExpenseDocuments" (
    "Id"                UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"         UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE RESTRICT,
    "ExpenseUploadId"   UUID        REFERENCES "ExpenseUploads"("Id") ON DELETE SET NULL,
    "SupplierId"        UUID        REFERENCES "Suppliers"("Id") ON DELETE SET NULL,
    "JournalEntryId"    UUID,
    -- Datos extraidos por OCR / introducidos manualmente
    "InvoiceNumber"     TEXT,
    "SupplierName"      TEXT,
    "SupplierTaxId"     TEXT,
    "IssueDate"         TIMESTAMPTZ,
    "TaxBase"           NUMERIC,
    "VatRate"           NUMERIC     NOT NULL DEFAULT 21,
    "VatAmount"         NUMERIC,
    "IrpfRate"          NUMERIC     NOT NULL DEFAULT 0,
    "IrpfAmount"        NUMERIC,
    "Total"             NUMERIC,
    -- Confianza OCR
    "OcrConfidence"     NUMERIC,
    -- Flujo de trabajo
    "Status"            TEXT        NOT NULL DEFAULT 'Draft', -- Draft | Reviewed | Approved | Rejected
    "IsValidated"       BOOLEAN     NOT NULL DEFAULT false,
    "ValidatedAt"       TIMESTAMPTZ,
    "ValidatedBy"       TEXT,
    -- Cumplimiento Ley Antifraude
    "HashSignature"     TEXT,       -- SHA256 del documento
    "IsLocked"          BOOLEAN     NOT NULL DEFAULT false,
    "LockedAt"          TIMESTAMPTZ,
    "CreatedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"         TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_ExpenseDocuments_CompanyId"         ON "ExpenseDocuments"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_ExpenseDocuments_Status"            ON "ExpenseDocuments"("Status");
CREATE INDEX IF NOT EXISTS "IX_ExpenseDocuments_SupplierId"        ON "ExpenseDocuments"("SupplierId");
CREATE INDEX IF NOT EXISTS "IX_ExpenseDocuments_ExpenseUploadId"   ON "ExpenseDocuments"("ExpenseUploadId");

-- ============================================================
-- CONTABILIDAD (Accounting)
-- Plan General Contable espanol (PGC 2008) - partida doble
-- ============================================================

CREATE TABLE IF NOT EXISTS "ChartOfAccounts" (
    "Id"          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID    NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Code"        TEXT    NOT NULL,
    "Name"        TEXT    NOT NULL,
    "AccountType" TEXT    NOT NULL DEFAULT 'Asset', -- Asset | Liability | Equity | Revenue | Expense
    "IsSystem"    BOOLEAN NOT NULL DEFAULT false,
    CONSTRAINT "UQ_ChartOfAccounts_Company_Code" UNIQUE ("CompanyId", "Code")
);
CREATE INDEX IF NOT EXISTS "IX_ChartOfAccounts_CompanyId" ON "ChartOfAccounts"("CompanyId");

CREATE TABLE IF NOT EXISTS "JournalEntries" (
    "Id"            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"     UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE RESTRICT,
    "Date"          TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "Reference"     TEXT        NOT NULL,
    "Description"   TEXT        NOT NULL DEFAULT '',
    "SourceType"    TEXT,       -- Invoice | Expense | Manual
    "SourceId"      UUID,
    "IsPosted"      BOOLEAN     NOT NULL DEFAULT false,
    "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_JournalEntries_CompanyId" ON "JournalEntries"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_JournalEntries_Date"      ON "JournalEntries"("Date");
CREATE INDEX IF NOT EXISTS "IX_JournalEntries_SourceId"  ON "JournalEntries"("SourceId");

CREATE TABLE IF NOT EXISTS "JournalEntryLines" (
    "Id"             UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "JournalEntryId" UUID    NOT NULL REFERENCES "JournalEntries"("Id") ON DELETE RESTRICT,
    "AccountCode"    TEXT    NOT NULL,
    "AccountName"    TEXT    NOT NULL DEFAULT '',
    "Debit"          NUMERIC NOT NULL DEFAULT 0,
    "Credit"         NUMERIC NOT NULL DEFAULT 0,
    "Description"    TEXT
);
CREATE INDEX IF NOT EXISTS "IX_JournalEntryLines_JournalEntryId" ON "JournalEntryLines"("JournalEntryId");
CREATE INDEX IF NOT EXISTS "IX_JournalEntryLines_AccountCode"    ON "JournalEntryLines"("AccountCode");

-- ============================================================
-- INVENTARIO (Inventory)
-- ============================================================

CREATE TABLE IF NOT EXISTS "Warehouses" (
    "Id"        UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId" UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"      TEXT        NOT NULL,
    "Location"  TEXT,
    "IsActive"  BOOLEAN     NOT NULL DEFAULT true,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Warehouses_CompanyId" ON "Warehouses"("CompanyId");

CREATE TABLE IF NOT EXISTS "Products" (
    "Id"          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"        TEXT        NOT NULL,
    "Sku"         TEXT        NOT NULL DEFAULT '',
    "Description" TEXT,
    "Price"       NUMERIC     NOT NULL DEFAULT 0,
    "TaxRate"     NUMERIC     NOT NULL DEFAULT 21,
    "IsActive"    BOOLEAN     NOT NULL DEFAULT true,
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "UpdatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_Products_CompanyId" ON "Products"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Products_Sku"       ON "Products"("CompanyId", "Sku");

CREATE TABLE IF NOT EXISTS "Stocks" (
    "Id"          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID    NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "ProductId"   UUID    NOT NULL REFERENCES "Products"("Id") ON DELETE CASCADE,
    "WarehouseId" UUID    REFERENCES "Warehouses"("Id") ON DELETE SET NULL,
    "Quantity"    NUMERIC NOT NULL DEFAULT 0,
    CONSTRAINT "UQ_Stocks_Product_Warehouse" UNIQUE ("ProductId", "WarehouseId"),
    CONSTRAINT "CK_Stock_Quantity_NonNegative" CHECK ("Quantity" >= 0)
);
CREATE INDEX IF NOT EXISTS "IX_Stocks_CompanyId"   ON "Stocks"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_Stocks_ProductId"   ON "Stocks"("ProductId");
CREATE INDEX IF NOT EXISTS "IX_Stocks_WarehouseId" ON "Stocks"("WarehouseId");

CREATE TABLE IF NOT EXISTS "StockMovements" (
    "Id"            UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"     UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE RESTRICT,
    "ProductId"     UUID        NOT NULL REFERENCES "Products"("Id")  ON DELETE RESTRICT,
    "WarehouseId"   UUID        REFERENCES "Warehouses"("Id")         ON DELETE SET NULL,
    "Type"          TEXT        NOT NULL,   -- In | Out | Transfer | Adjustment
    "Quantity"      NUMERIC     NOT NULL,
    "Reference"     TEXT,
    "Notes"         TEXT,
    "CreatedAt"     TIMESTAMPTZ NOT NULL DEFAULT NOW()
    -- Sin UpdatedAt: inmutable por diseno (trazabilidad)
);
CREATE INDEX IF NOT EXISTS "IX_StockMovements_CompanyId"  ON "StockMovements"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_StockMovements_ProductId"  ON "StockMovements"("ProductId");
CREATE INDEX IF NOT EXISTS "IX_StockMovements_CreatedAt"  ON "StockMovements"("CreatedAt");

-- ============================================================
-- API PUBLICA
-- ============================================================

CREATE TABLE IF NOT EXISTS "ApiKeys" (
    "Id"          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "Name"        TEXT        NOT NULL,
    "KeyHash"     TEXT        NOT NULL UNIQUE,
    "RateLimit"   INT         NOT NULL DEFAULT 60,
    "IsActive"    BOOLEAN     NOT NULL DEFAULT true,
    "LastUsedAt"  TIMESTAMPTZ,
    "ExpiresAt"   TIMESTAMPTZ,
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_ApiKeys_CompanyId" ON "ApiKeys"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_ApiKeys_KeyHash"   ON "ApiKeys"("KeyHash");

-- ============================================================
-- EVENTOS / OUTBOX PATTERN
-- ============================================================

CREATE TABLE IF NOT EXISTS "OutboxMessages" (
    "Id"         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"  UUID        NOT NULL,
    "EventType"  TEXT        NOT NULL,
    "Payload"    TEXT        NOT NULL,
    "CreatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ProcessedAt" TIMESTAMPTZ,
    "Error"      TEXT,
    "RetryCount" INT         NOT NULL DEFAULT 0,
    "Status"     INT         NOT NULL DEFAULT 0,  -- 0=Pending, 1=Done, 2=Failed
    "RelayError" TEXT,
    "RelayedAt"  TIMESTAMPTZ,
    "Exchange"   TEXT,
    "RoutingKey" TEXT
);
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Status"    ON "OutboxMessages"("Status");
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_CreatedAt" ON "OutboxMessages"("CreatedAt");
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Relay"
    ON "OutboxMessages"("Status", "RetryCount", "CreatedAt")
    WHERE "Status" = 0;
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending_Relay"
    ON "OutboxMessages"("Status", "RelayedAt")
    WHERE "Status" = 0;

-- ============================================================
-- SII - SUMINISTRO INMEDIATO DE INFORMACION (AEAT)
-- RD 596/2016
-- ============================================================

CREATE TABLE IF NOT EXISTS "SiiSubmissions" (
    "Id"          UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"   UUID        NOT NULL REFERENCES "Companies"("Id") ON DELETE CASCADE,
    "BookType"    TEXT        NOT NULL,  -- LRFE | LRFR
    "PeriodYear"  INT         NOT NULL,
    "PeriodMonth" INT         NOT NULL,
    "Status"      TEXT        NOT NULL DEFAULT 'pending',
    "XmlPayload"  TEXT,
    "SubmittedAt" TIMESTAMPTZ,
    "Error"       TEXT,
    "CreatedAt"   TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_SiiSubmissions_CompanyId" ON "SiiSubmissions"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_SiiSubmissions_Period"    ON "SiiSubmissions"("CompanyId", "PeriodYear", "PeriodMonth");

-- ============================================================
-- AUDIT LOG (inmutable)
-- ============================================================

CREATE TABLE IF NOT EXISTS "AuditLogs" (
    "Id"         UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId"  UUID        NOT NULL,
    "UserId"     UUID,
    "Action"     TEXT        NOT NULL,
    "Entity"     TEXT        NOT NULL,
    "EntityId"   TEXT,
    "OldValues"  JSONB,
    "NewValues"  JSONB,
    "IpAddress"  TEXT,
    "UserAgent"  TEXT,
    "CreatedAt"  TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_CompanyId"  ON "AuditLogs"("CompanyId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_EntityId"   ON "AuditLogs"("EntityId");
CREATE INDEX IF NOT EXISTS "IX_AuditLogs_CreatedAt"  ON "AuditLogs"("CreatedAt");



-- ============================================================
-- TRIGGERS DE PROTECCION ANTIFRAUDE
-- Ley 11/2021 Antifraude + RD 1619/2012
-- Ninguna factura, asiento, linea de asiento, gasto aprobado
-- o log de auditoria puede ser borrado ni (en algunos casos) modificado.
-- ============================================================

-- -----------------------------------------------------------
-- 1. trg_no_delete_invoices
--    Invoices son documentos fiscales emitidos -> NO se borran.
--    Para anular: emitir factura rectificativa.
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_invoice_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "Invoices" is forbidden by Ley 11/2021 Antifraude. '
        'Use a rectificative invoice (Rectificativa) to void an invoice.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_invoices ON "Invoices";
CREATE TRIGGER trg_no_delete_invoices
    BEFORE DELETE ON "Invoices"
    FOR EACH ROW EXECUTE FUNCTION prevent_invoice_delete();

-- -----------------------------------------------------------
-- 2. trg_no_delete_journalentries
--    Asientos contables son inmutables (PGC, Ley 11/2021).
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_journalentry_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "JournalEntries" is forbidden by Ley 11/2021 Antifraude. '
        'Accounting entries are immutable. Create a reversal entry instead.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_journalentries ON "JournalEntries";
CREATE TRIGGER trg_no_delete_journalentries
    BEFORE DELETE ON "JournalEntries"
    FOR EACH ROW EXECUTE FUNCTION prevent_journalentry_delete();

-- -----------------------------------------------------------
-- 3. trg_no_delete_journallines
--    Lineas de asiento son parte del diario fiscal.
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_journalline_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "JournalEntryLines" is forbidden by Ley 11/2021 Antifraude.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_journallines ON "JournalEntryLines";
CREATE TRIGGER trg_no_delete_journallines
    BEFORE DELETE ON "JournalEntryLines"
    FOR EACH ROW EXECUTE FUNCTION prevent_journalline_delete();

-- -----------------------------------------------------------
-- 4. trg_no_update_stockmovements
--    Movimientos de stock son inmutables (trazabilidad de inventario).
--    Para corregir: crear movimiento de ajuste compensatorio.
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_stockmovement_update()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'UPDATE on "StockMovements" is forbidden. '
        'Stock movements are immutable for audit trail purposes. '
        'Create a compensating Adjustment movement instead.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_update_stockmovements ON "StockMovements";
CREATE TRIGGER trg_no_update_stockmovements
    BEFORE UPDATE ON "StockMovements"
    FOR EACH ROW EXECUTE FUNCTION prevent_stockmovement_update();

-- DELETE on StockMovements tambien prohibido
CREATE OR REPLACE FUNCTION prevent_stockmovement_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "StockMovements" is forbidden. '
        'Stock movements are immutable for audit trail purposes.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_stockmovements ON "StockMovements";
CREATE TRIGGER trg_no_delete_stockmovements
    BEFORE DELETE ON "StockMovements"
    FOR EACH ROW EXECUTE FUNCTION prevent_stockmovement_delete();

-- -----------------------------------------------------------
-- 5. trg_no_delete_auditlogs
--    Los logs de auditoria no pueden borrarse (RGPD art. 5 + Ley 11/2021).
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_auditlog_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "AuditLogs" is forbidden by Ley 11/2021 Antifraude and RGPD art. 5.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_auditlogs ON "AuditLogs";
CREATE TRIGGER trg_no_delete_auditlogs
    BEFORE DELETE ON "AuditLogs"
    FOR EACH ROW EXECUTE FUNCTION prevent_auditlog_delete();

-- -----------------------------------------------------------
-- 6. trg_no_delete_permissions
--    Permisos del sistema no pueden borrarse (integridad ABAC).
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_permissions_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION
        'DELETE on "Permissions" is forbidden. '
        'Use soft-delete or revoke role/user assignments instead.';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_permissions ON "Permissions";
CREATE TRIGGER trg_no_delete_permissions
    BEFORE DELETE ON "Permissions"
    FOR EACH ROW EXECUTE FUNCTION prevent_permissions_delete();

-- -----------------------------------------------------------
-- 7. trg_no_delete_expensedocuments_approved
--    Gastos aprobados (contabilizados) son inmutables.
-- -----------------------------------------------------------
CREATE OR REPLACE FUNCTION prevent_approved_expense_delete()
RETURNS TRIGGER AS $$
BEGIN
    IF OLD."IsLocked" = true OR OLD."Status" = 'Approved' THEN
        RAISE EXCEPTION
            'DELETE on approved/locked "ExpenseDocuments" is forbidden by Ley 11/2021 Antifraude.';
    END IF;
    RETURN OLD;
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_expensedocuments_approved ON "ExpenseDocuments";
CREATE TRIGGER trg_no_delete_expensedocuments_approved
    BEFORE DELETE ON "ExpenseDocuments"
    FOR EACH ROW EXECUTE FUNCTION prevent_approved_expense_delete();



-- ============================================================
-- CONSTRAINTS ADICIONALES
-- ============================================================

-- Stock no negativo (redundante con constraint de tabla, pero explicitado)
ALTER TABLE "Stocks" DROP CONSTRAINT IF EXISTS "CK_Stock_Quantity_NonNegative";
ALTER TABLE "Stocks" ADD CONSTRAINT "CK_Stock_Quantity_NonNegative" CHECK ("Quantity" >= 0);

-- Facturas: total consistente (soft check para alertar en migraciones)
-- No se usa CHECK para flexibilidad con redondeos, pero si un índice parcial
-- de vigilancia para detectar inconsistencias:
-- (opcional, comentado por defecto)
-- CREATE INDEX IF NOT EXISTS "IX_Invoices_NegativeTotal" ON "Invoices"("Total") WHERE "Total" < 0;



-- ============================================================
-- ABAC PERMISSIONS SEED
-- (Ejecutar despues de crear la tabla Permissions)
-- ============================================================

INSERT INTO "Permissions" ("Resource", "Action", "Description", "Code") VALUES
    ('Invoice',        'Create',      'Crear facturas',                           'Invoice:Create'),
    ('Invoice',        'Read',        'Ver facturas',                             'Invoice:Read'),
    ('Invoice',        'Update',      'Editar facturas en borrador',              'Invoice:Update'),
    ('Invoice',        'Delete',      'Anular facturas (rectificativa)',           'Invoice:Delete'),
    ('Invoice',        'Lock',        'Bloquear/emitir facturas',                 'Invoice:Lock'),
    ('Invoice',        'Approve',     'Aprobar facturas',                         'Invoice:Approve'),
    ('Expense',        'Create',      'Crear gastos',                             'Expense:Create'),
    ('Expense',        'Read',        'Ver gastos',                               'Expense:Read'),
    ('Expense',        'Update',      'Editar gastos en borrador',                'Expense:Update'),
    ('Expense',        'Approve',     'Aprobar gastos y generar asiento',         'Expense:Approve'),
    ('Client',         'Create',      'Crear clientes',                           'Client:Create'),
    ('Client',         'Read',        'Ver clientes',                             'Client:Read'),
    ('Client',         'Update',      'Editar clientes',                          'Client:Update'),
    ('Client',         'Delete',      'Eliminar clientes',                        'Client:Delete'),
    ('Accounting',     'Read',        'Ver contabilidad y reportes',              'Accounting:Read'),
    ('Accounting',     'ManualEntry', 'Crear asientos manuales',                  'Accounting:ManualEntry'),
    ('UserManagement', 'Read',        'Ver usuarios',                             'UserManagement:Read'),
    ('UserManagement', 'Write',       'Crear y editar usuarios',                  'UserManagement:Write'),
    ('UserManagement', 'Delete',      'Eliminar usuarios',                        'UserManagement:Delete'),
    ('Inventory',      'Read',        'Ver inventario',                           'Inventory:Read'),
    ('Inventory',      'Write',       'Gestionar stock y productos',              'Inventory:Write'),
    ('Report',         'Export',      'Exportar datos (SII, Excel, PDF)',          'Report:Export')
ON CONFLICT ("Resource", "Action") DO NOTHING;



-- ============================================================
-- SUMMARY DE TRIGGERS INSTALADOS
-- ============================================================
/*
  TRIGGER                                TABLE                STATUS
  -----------------------------------------------------------------------
  trg_no_delete_invoices                 Invoices             BEFORE DELETE -> ERROR
  trg_no_delete_journalentries           JournalEntries       BEFORE DELETE -> ERROR
  trg_no_delete_journallines             JournalEntryLines    BEFORE DELETE -> ERROR
  trg_no_update_stockmovements           StockMovements       BEFORE UPDATE -> ERROR
  trg_no_delete_stockmovements           StockMovements       BEFORE DELETE -> ERROR
  trg_no_delete_auditlogs               AuditLogs            BEFORE DELETE -> ERROR
  trg_no_delete_permissions             Permissions          BEFORE DELETE -> ERROR
  trg_no_delete_expensedocuments_approved ExpenseDocuments   BEFORE DELETE (if locked/approved) -> ERROR
  -----------------------------------------------------------------------

  Para verificar triggers instalados:
    SELECT trigger_name, event_object_table, event_manipulation, action_timing
    FROM information_schema.triggers
    WHERE trigger_schema = 'public'
    ORDER BY event_object_table, trigger_name;
*/

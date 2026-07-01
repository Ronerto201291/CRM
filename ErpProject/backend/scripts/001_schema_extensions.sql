-- ================================================================
-- ERP SaaS - Schema Extensions (Database-First)
-- Run manually on your PostgreSQL instance
-- ================================================================

-- 2FA: Add TOTP columns to Users
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "TotpSecret" TEXT NULL;
ALTER TABLE "Users" ADD COLUMN IF NOT EXISTS "TotpBackupCodes" TEXT NULL;

-- Stripe: Add Stripe IDs to Companies and Subscriptions
ALTER TABLE "Companies" ADD COLUMN IF NOT EXISTS "StripeCustomerId" TEXT NULL;
ALTER TABLE "Subscriptions" ADD COLUMN IF NOT EXISTS "StripeSubscriptionId" TEXT NULL;
ALTER TABLE "Subscriptions" ADD COLUMN IF NOT EXISTS "StripeStatus" TEXT NOT NULL DEFAULT 'inactive';

-- Plans table (if not exists from previous migration)
CREATE TABLE IF NOT EXISTS "Plans" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "Name" TEXT NOT NULL,
    "Description" TEXT NOT NULL DEFAULT '',
    "MonthlyPrice" NUMERIC NOT NULL DEFAULT 0,
    "YearlyPrice" NUMERIC NOT NULL DEFAULT 0,
    "MaxUsers" INT NOT NULL DEFAULT 1,
    "MaxInvoicesPerMonth" INT NOT NULL DEFAULT 50,
    "IsActive" BOOLEAN NOT NULL DEFAULT true,
    "SortOrder" INT NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS "PlanModules" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "PlanId" UUID NOT NULL REFERENCES "Plans"("Id") ON DELETE CASCADE,
    "ModuleName" TEXT NOT NULL,
    "IsIncluded" BOOLEAN NOT NULL DEFAULT true
);

-- OutboxMessages table
CREATE TABLE IF NOT EXISTS "OutboxMessages" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId" UUID NOT NULL,
    "EventType" TEXT NOT NULL,
    "Payload" TEXT NOT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW(),
    "ProcessedAt" TIMESTAMPTZ NULL,
    "Error" TEXT NULL,
    "RetryCount" INT NOT NULL DEFAULT 0,
    "Status" INT NOT NULL DEFAULT 0
);
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Status" ON "OutboxMessages"("Status");
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_CreatedAt" ON "OutboxMessages"("CreatedAt");

-- SII submissions tracking
CREATE TABLE IF NOT EXISTS "SiiSubmissions" (
    "Id" UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    "CompanyId" UUID NOT NULL,
    "BookType" TEXT NOT NULL,
    "PeriodYear" INT NOT NULL,
    "PeriodMonth" INT NOT NULL,
    "Status" TEXT NOT NULL DEFAULT 'pending',
    "XmlPayload" TEXT NULL,
    "SubmittedAt" TIMESTAMPTZ NULL,
    "Error" TEXT NULL,
    "CreatedAt" TIMESTAMPTZ NOT NULL DEFAULT NOW()
);
CREATE INDEX IF NOT EXISTS "IX_SiiSubmissions_CompanyId" ON "SiiSubmissions"("CompanyId");

-- Stock non-negative constraint
ALTER TABLE "Stocks" DROP CONSTRAINT IF EXISTS "CK_Stock_Quantity_NonNegative";
ALTER TABLE "Stocks" ADD CONSTRAINT "CK_Stock_Quantity_NonNegative" CHECK ("Quantity" >= 0);

-- Triggers: BEFORE DELETE on JournalEntryLines
CREATE OR REPLACE FUNCTION prevent_journalline_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'DELETE on JournalEntryLines is forbidden by Ley 11/2021 Antifraude';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_journallines ON "JournalEntryLines";
CREATE TRIGGER trg_no_delete_journallines
BEFORE DELETE ON "JournalEntryLines"
FOR EACH ROW EXECUTE FUNCTION prevent_journalline_delete();

-- Triggers: BEFORE DELETE on AuditLogs
CREATE OR REPLACE FUNCTION prevent_auditlog_delete()
RETURNS TRIGGER AS $$
BEGIN
    RAISE EXCEPTION 'DELETE on AuditLogs is forbidden by Ley 11/2021 Antifraude';
END;
$$ LANGUAGE plpgsql;

DROP TRIGGER IF EXISTS trg_no_delete_auditlogs ON "AuditLogs";
CREATE TRIGGER trg_no_delete_auditlogs
BEFORE DELETE ON "AuditLogs"
FOR EACH ROW EXECUTE FUNCTION prevent_auditlog_delete();

-- ERP Schema Migration — run ONCE on existing databases
-- Moves module tables from the 'public' schema to their dedicated schemas.
-- Prerequisites:
--   1. Run init-schemas.sql first (or schemas already exist)
--   2. Stop the application before running this script
--   3. After running, mark module InitialCreate migrations as applied:
--      INSERT INTO billing."__EFMigrationsHistory" VALUES ('20260329000000_InitialCreate', '10.0.3');
--      INSERT INTO crm."__EFMigrationsHistory" VALUES ('20260329000000_InitialCreate', '10.0.3');
--      INSERT INTO inventory."__EFMigrationsHistory" VALUES ('20260329000000_InitialCreate', '10.0.3');
--      INSERT INTO accounting."__EFMigrationsHistory" VALUES ('20260329000000_InitialCreate', '10.0.3');
--      INSERT INTO expenses."__EFMigrationsHistory" VALUES ('20260329000000_InitialCreate', '10.0.3');

BEGIN;

-- Billing
ALTER TABLE IF EXISTS public."BillingInvoices" SET SCHEMA billing;
ALTER TABLE IF EXISTS public."BillingInvoiceLines" SET SCHEMA billing;
ALTER TABLE IF EXISTS public."Quotes" SET SCHEMA billing;

-- CRM
ALTER TABLE IF EXISTS public."Clients" SET SCHEMA crm;
ALTER TABLE IF EXISTS public."Leads" SET SCHEMA crm;
ALTER TABLE IF EXISTS public."Suppliers" SET SCHEMA crm;
ALTER TABLE IF EXISTS public."Contacts" SET SCHEMA crm;
ALTER TABLE IF EXISTS public."ActivityLogs" SET SCHEMA crm;

-- Inventory
ALTER TABLE IF EXISTS public."InventoryProducts" SET SCHEMA inventory;
ALTER TABLE IF EXISTS public."Warehouses" SET SCHEMA inventory;
ALTER TABLE IF EXISTS public."Stocks" SET SCHEMA inventory;
ALTER TABLE IF EXISTS public."StockMovements" SET SCHEMA inventory;

-- Accounting
ALTER TABLE IF EXISTS public."Accounts" SET SCHEMA accounting;
ALTER TABLE IF EXISTS public."JournalEntries" SET SCHEMA accounting;
ALTER TABLE IF EXISTS public."JournalEntryLines" SET SCHEMA accounting;

-- Expenses
ALTER TABLE IF EXISTS public."ExpenseUploads" SET SCHEMA expenses;
ALTER TABLE IF EXISTS public."ExpenseDocuments" SET SCHEMA expenses;
ALTER TABLE IF EXISTS public."ExpenseDocumentLines" SET SCHEMA expenses;
ALTER TABLE IF EXISTS public."AccountingEntries" SET SCHEMA expenses;

COMMIT;

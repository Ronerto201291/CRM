CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Accounts" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Code" text NOT NULL,
        "Name" text NOT NULL,
        "Type" text NOT NULL,
        CONSTRAINT "PK_Accounts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "ApiKeys" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Key" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "RateLimit" integer NOT NULL,
        CONSTRAINT "PK_ApiKeys" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Clients" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "TaxId" text NOT NULL,
        "Email" text NOT NULL,
        "Phone" text NOT NULL,
        "Address" text NOT NULL,
        "CustomFields" jsonb NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Clients" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Companies" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "TaxId" text NOT NULL,
        "Address" text NOT NULL,
        "Country" text NOT NULL,
        "SubscriptionId" uuid NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Companies" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "JournalEntries" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Date" timestamp with time zone NOT NULL,
        "Reference" text NOT NULL,
        CONSTRAINT "PK_JournalEntries" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Leads" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Status" text NOT NULL,
        "Source" text NOT NULL,
        "Notes" text NOT NULL,
        CONSTRAINT "PK_Leads" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Permissions" (
        "Id" uuid NOT NULL,
        "Code" text NOT NULL,
        "Description" text NOT NULL,
        CONSTRAINT "PK_Permissions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Products" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "SKU" text NOT NULL,
        "Price" numeric NOT NULL,
        "TaxRate" numeric NOT NULL,
        "Stock" numeric NOT NULL,
        CONSTRAINT "PK_Products" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Quotes" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Status" text NOT NULL,
        "ExpirationDate" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Quotes" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Rules" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "TriggerEvent" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Rules" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Subscriptions" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PlanName" text NOT NULL,
        "ExpirationDate" timestamp with time zone NOT NULL,
        "ActiveModules" jsonb NOT NULL,
        "IsActive" boolean NOT NULL,
        CONSTRAINT "PK_Subscriptions" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "TaxReports" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Period" text NOT NULL,
        "TotalCollected" numeric NOT NULL,
        "TotalPaid" numeric NOT NULL,
        "NetTax" numeric NOT NULL,
        CONSTRAINT "PK_TaxReports" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "ApiUsageLogs" (
        "Id" uuid NOT NULL,
        "ApiKeyId" uuid NOT NULL,
        "Endpoint" text NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ApiUsageLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ApiUsageLogs_ApiKeys_ApiKeyId" FOREIGN KEY ("ApiKeyId") REFERENCES "ApiKeys" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Invoices" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ClientId" uuid NOT NULL,
        "Number" text NOT NULL,
        "Series" text NOT NULL,
        "IssueDate" timestamp with time zone NOT NULL,
        "DueDate" timestamp with time zone NOT NULL,
        "Subtotal" numeric NOT NULL,
        "TaxAmount" numeric NOT NULL,
        "Total" numeric NOT NULL,
        "Status" text NOT NULL,
        "IsLocked" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Invoices" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Invoices_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Roles" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        CONSTRAINT "PK_Roles" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Roles_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Email" text NOT NULL,
        "PasswordHash" text NOT NULL,
        "FirstName" text NOT NULL,
        "LastName" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "TwoFactorEnabled" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Users_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "JournalEntryLines" (
        "Id" uuid NOT NULL,
        "JournalEntryId" uuid NOT NULL,
        "AccountId" uuid NOT NULL,
        "Debit" numeric NOT NULL,
        "Credit" numeric NOT NULL,
        CONSTRAINT "PK_JournalEntryLines" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_JournalEntryLines_Accounts_AccountId" FOREIGN KEY ("AccountId") REFERENCES "Accounts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_JournalEntryLines_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "StockMovements" (
        "Id" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "Type" text NOT NULL,
        "Quantity" numeric NOT NULL,
        "Date" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StockMovements_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Actions" (
        "Id" uuid NOT NULL,
        "RuleId" uuid NOT NULL,
        "Type" text NOT NULL,
        "Configuration" jsonb NOT NULL,
        "ExecutionOrder" integer NOT NULL,
        CONSTRAINT "PK_Actions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Actions_Rules_RuleId" FOREIGN KEY ("RuleId") REFERENCES "Rules" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "Conditions" (
        "Id" uuid NOT NULL,
        "RuleId" uuid NOT NULL,
        "Field" text NOT NULL,
        "Operator" text NOT NULL,
        "Value" text NOT NULL,
        CONSTRAINT "PK_Conditions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Conditions_Rules_RuleId" FOREIGN KEY ("RuleId") REFERENCES "Rules" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "InvoiceLines" (
        "Id" uuid NOT NULL,
        "InvoiceId" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "Description" text NOT NULL,
        "Quantity" numeric NOT NULL,
        "UnitPrice" numeric NOT NULL,
        "TaxRate" numeric NOT NULL,
        "LineTotal" numeric NOT NULL,
        CONSTRAINT "PK_InvoiceLines" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_InvoiceLines_Invoices_InvoiceId" FOREIGN KEY ("InvoiceId") REFERENCES "Invoices" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_InvoiceLines_Products_ProductId" FOREIGN KEY ("ProductId") REFERENCES "Products" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "RolePermissions" (
        "RoleId" uuid NOT NULL,
        "PermissionId" uuid NOT NULL,
        CONSTRAINT "PK_RolePermissions" PRIMARY KEY ("RoleId", "PermissionId"),
        CONSTRAINT "FK_RolePermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_RolePermissions_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE TABLE "AuditLogs" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Entity" text NOT NULL,
        "Action" text NOT NULL,
        "Timestamp" timestamp with time zone NOT NULL,
        "OldValues" jsonb NOT NULL,
        "NewValues" jsonb NOT NULL,
        CONSTRAINT "PK_AuditLogs" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AuditLogs_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_Actions_RuleId" ON "Actions" ("RuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_ApiUsageLogs_ApiKeyId" ON "ApiUsageLogs" ("ApiKeyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_AuditLogs_UserId" ON "AuditLogs" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_Conditions_RuleId" ON "Conditions" ("RuleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_InvoiceLines_InvoiceId" ON "InvoiceLines" ("InvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_InvoiceLines_ProductId" ON "InvoiceLines" ("ProductId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_Invoices_ClientId" ON "Invoices" ("ClientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_JournalEntryLines_AccountId" ON "JournalEntryLines" ("AccountId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_JournalEntryLines_JournalEntryId" ON "JournalEntryLines" ("JournalEntryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_RolePermissions_PermissionId" ON "RolePermissions" ("PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_Roles_CompanyId" ON "Roles" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_StockMovements_ProductId" ON "StockMovements" ("ProductId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    CREATE INDEX "IX_Users_CompanyId" ON "Users" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260227125614_InitialCreate') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260227125614_InitialCreate', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE TABLE "Plans" (
        "Id" uuid NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "MonthlyPrice" numeric NOT NULL,
        "YearlyPrice" numeric NOT NULL,
        "MaxUsers" integer NOT NULL,
        "MaxInvoicesPerMonth" integer NOT NULL,
        "IsActive" boolean NOT NULL,
        "SortOrder" integer NOT NULL,
        CONSTRAINT "PK_Plans" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE TABLE "PlanModules" (
        "Id" uuid NOT NULL,
        "PlanId" uuid NOT NULL,
        "ModuleName" text NOT NULL,
        "IsIncluded" boolean NOT NULL,
        CONSTRAINT "PK_PlanModules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_PlanModules_Plans_PlanId" FOREIGN KEY ("PlanId") REFERENCES "Plans" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE INDEX "IX_PlanModules_PlanId" ON "PlanModules" ("PlanId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE TABLE "OutboxMessages" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "EventType" text NOT NULL,
        "Payload" text NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "ProcessedAt" timestamp with time zone,
        "Error" text,
        "RetryCount" integer NOT NULL,
        "Status" integer NOT NULL,
        CONSTRAINT "PK_OutboxMessages" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE INDEX "IX_OutboxMessages_Status" ON "OutboxMessages" ("Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    CREATE INDEX "IX_OutboxMessages_CreatedAt" ON "OutboxMessages" ("CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN

                    CREATE OR REPLACE FUNCTION prevent_journalline_delete()
                    RETURNS TRIGGER AS $$
                    BEGIN
                        RAISE EXCEPTION 'DELETE on JournalEntryLines is forbidden by Ley 11/2021 Antifraude';
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER trg_no_delete_journallines
                    BEFORE DELETE ON public."JournalEntryLines"
                    FOR EACH ROW EXECUTE FUNCTION prevent_journalline_delete();
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN

                    CREATE OR REPLACE FUNCTION prevent_auditlog_delete()
                    RETURNS TRIGGER AS $$
                    BEGIN
                        RAISE EXCEPTION 'DELETE on AuditLogs is forbidden by Ley 11/2021 Antifraude';
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER trg_no_delete_auditlogs
                    BEFORE DELETE ON public."AuditLogs"
                    FOR EACH ROW EXECUTE FUNCTION prevent_auditlog_delete();
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN

                    INSERT INTO "Plans" ("Id", "Name", "Description", "MonthlyPrice", "YearlyPrice", "MaxUsers", "MaxInvoicesPerMonth", "IsActive", "SortOrder")
                    VALUES
                      (gen_random_uuid(), 'Free',         'Plan gratuito: facturación básica',          0,     0,     1,   20,  true, 1),
                      (gen_random_uuid(), 'Starter',      'Para autónomos y pequeñas empresas',        19,   190,    3,  100,  true, 2),
                      (gen_random_uuid(), 'Professional', 'Para PYMES con contabilidad completa',      49,   490,   10,  500,  true, 3),
                      (gen_random_uuid(), 'Enterprise',   'Sin límites, API pública y OCR avanzado',  99,   990, 9999, 9999, true, 4);

                    -- Modules per plan
                    INSERT INTO "PlanModules" ("Id", "PlanId", "ModuleName", "IsIncluded")
                    SELECT gen_random_uuid(), p."Id", m."ModuleName", m."IsIncluded"
                    FROM "Plans" p
                    CROSS JOIN (
                        SELECT 'Free' as plan, 'Billing' as "ModuleName", true as "IsIncluded"
                        UNION ALL SELECT 'Free', 'CRM', true
                        UNION ALL SELECT 'Free', 'Expenses', false
                        UNION ALL SELECT 'Free', 'Accounting', false
                        UNION ALL SELECT 'Free', 'Inventory', false
                        UNION ALL SELECT 'Free', 'OCR', false
                        UNION ALL SELECT 'Free', 'PublicApi', false
                        UNION ALL SELECT 'Starter', 'Billing', true
                        UNION ALL SELECT 'Starter', 'CRM', true
                        UNION ALL SELECT 'Starter', 'Expenses', true
                        UNION ALL SELECT 'Starter', 'Accounting', true
                        UNION ALL SELECT 'Starter', 'Inventory', false
                        UNION ALL SELECT 'Starter', 'OCR', false
                        UNION ALL SELECT 'Starter', 'PublicApi', false
                        UNION ALL SELECT 'Professional', 'Billing', true
                        UNION ALL SELECT 'Professional', 'CRM', true
                        UNION ALL SELECT 'Professional', 'Expenses', true
                        UNION ALL SELECT 'Professional', 'Accounting', true
                        UNION ALL SELECT 'Professional', 'Inventory', true
                        UNION ALL SELECT 'Professional', 'OCR', true
                        UNION ALL SELECT 'Professional', 'PublicApi', false
                        UNION ALL SELECT 'Enterprise', 'Billing', true
                        UNION ALL SELECT 'Enterprise', 'CRM', true
                        UNION ALL SELECT 'Enterprise', 'Expenses', true
                        UNION ALL SELECT 'Enterprise', 'Accounting', true
                        UNION ALL SELECT 'Enterprise', 'Inventory', true
                        UNION ALL SELECT 'Enterprise', 'OCR', true
                        UNION ALL SELECT 'Enterprise', 'PublicApi', true
                    ) m
                    WHERE p."Name" = m.plan;
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260301000000_AddLicensingOutboxAndConstraints') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260301000000_AddLicensingOutboxAndConstraints', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310000000_FixInvoiceLinesProductFK') THEN
    ALTER TABLE "InvoiceLines" DROP CONSTRAINT "FK_InvoiceLines_Products_ProductId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310000000_FixInvoiceLinesProductFK') THEN
    DROP INDEX "IX_InvoiceLines_ProductId";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310000000_FixInvoiceLinesProductFK') THEN
    ALTER TABLE "InvoiceLines" ALTER COLUMN "ProductId" TYPE uuid;
    ALTER TABLE "InvoiceLines" ALTER COLUMN "ProductId" DROP NOT NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310000000_FixInvoiceLinesProductFK') THEN
    CREATE INDEX "IX_InvoiceLines_ProductId" ON "InvoiceLines" ("ProductId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260310000000_FixInvoiceLinesProductFK') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260310000000_FixInvoiceLinesProductFK', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    DROP TABLE "StockMovements";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    DROP TABLE "Products";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE TABLE "InventoryProducts" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "SKU" text NOT NULL,
        "Name" text NOT NULL,
        "Description" text NOT NULL,
        "Type" text NOT NULL,
        "CostPrice" numeric NOT NULL,
        "SalePrice" numeric NOT NULL,
        "VatPercent" numeric NOT NULL,
        "TrackStock" boolean NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_InventoryProducts" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE INDEX "IX_InventoryProducts_CompanyId" ON "InventoryProducts" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE UNIQUE INDEX "UQ_InventoryProducts_CompanyId_SKU" ON "InventoryProducts" ("CompanyId", "SKU");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE TABLE "Warehouses" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Location" text NOT NULL,
        "IsActive" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Warehouses" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE INDEX "IX_Warehouses_CompanyId" ON "Warehouses" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE TABLE "Stocks" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "Quantity" numeric NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Stocks" PRIMARY KEY ("Id"),
        CONSTRAINT "CK_Stock_Quantity_NonNegative" CHECK ("Quantity" >= 0),
        CONSTRAINT "FK_Stocks_InventoryProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "InventoryProducts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_Stocks_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE INDEX "IX_Stocks_CompanyId" ON "Stocks" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE UNIQUE INDEX "UQ_Stocks_Product_Warehouse" ON "Stocks" ("ProductId", "WarehouseId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE TABLE "StockMovements" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ProductId" uuid NOT NULL,
        "WarehouseId" uuid NOT NULL,
        "MovementType" text NOT NULL,
        "Quantity" numeric NOT NULL,
        "UnitCost" numeric NOT NULL,
        "ReferenceType" text NOT NULL,
        "ReferenceId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_StockMovements_InventoryProducts_ProductId" FOREIGN KEY ("ProductId") REFERENCES "InventoryProducts" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_StockMovements_Warehouses_WarehouseId" FOREIGN KEY ("WarehouseId") REFERENCES "Warehouses" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE INDEX "IX_StockMovements_CompanyId" ON "StockMovements" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE INDEX "IX_StockMovements_ProductId" ON "StockMovements" ("ProductId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN

                    CREATE OR REPLACE FUNCTION prevent_stockmovement_mutation()
                    RETURNS TRIGGER AS $$
                    BEGIN
                        RAISE EXCEPTION 'StockMovements are immutable (Ley 11/2021 Antifraude)';
                    END;
                    $$ LANGUAGE plpgsql;

                    CREATE TRIGGER trg_no_update_stockmovements
                    BEFORE UPDATE OR DELETE ON public."StockMovements"
                    FOR EACH ROW EXECUTE FUNCTION prevent_stockmovement_mutation();
                
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE TABLE "TenantModules" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ModuleName" text NOT NULL,
        "IsEnabled" boolean NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_TenantModules" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_TenantModules_Companies_CompanyId" FOREIGN KEY ("CompanyId") REFERENCES "Companies" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    CREATE UNIQUE INDEX "UQ_TenantModules_Company_Module" ON "TenantModules" ("CompanyId", "ModuleName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321000000_InventoryModuleSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321000000_InventoryModuleSchema', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321010000_AddVerifactuToInvoice') THEN
    ALTER TABLE "Invoices" ADD "VerifactuHuella" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321010000_AddVerifactuToInvoice') THEN
    ALTER TABLE "Invoices" ADD "VerifactuQrUrl" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321010000_AddVerifactuToInvoice') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321010000_AddVerifactuToInvoice', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321020000_AddInventoryReorderPoint') THEN
    ALTER TABLE "InventoryProducts" ADD "ReorderPoint" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321020000_AddInventoryReorderPoint') THEN
    ALTER TABLE "InventoryProducts" ADD "ReorderQty" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321020000_AddInventoryReorderPoint') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321020000_AddInventoryReorderPoint', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_Invoices_CompanyId_Status" ON "Invoices" ("CompanyId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_Invoices_CompanyId_IssueDate" ON "Invoices" ("CompanyId", "IssueDate");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_JournalEntries_CompanyId_Date" ON "JournalEntries" ("CompanyId", "Date");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_AuditLogs_CompanyId_Timestamp" ON "AuditLogs" ("CompanyId", "Timestamp");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_StockMovements_CompanyId_CreatedAt" ON "StockMovements" ("CompanyId", "CreatedAt");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_Leads_CompanyId_Status" ON "Leads" ("CompanyId", "Status");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    CREATE INDEX "IX_Clients_CompanyId_Name" ON "Clients" ("CompanyId", "Name");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030000_AddCompositeIndexesMultiTenant') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321030000_AddCompositeIndexesMultiTenant', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321030001_AddOcrConfidence') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321030001_AddOcrConfidence', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    ALTER TABLE "ApiKeys" ADD "KeyPrefix" character varying(16) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    ALTER TABLE "ApiKeys" ADD "KeyHash" character varying(64) NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    CREATE EXTENSION IF NOT EXISTS pgcrypto;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    UPDATE "ApiKeys"
    SET "KeyPrefix" = left("Key", 8),
        "KeyHash"   = encode(digest("Key", 'sha256'), 'hex');
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    CREATE UNIQUE INDEX "IX_ApiKeys_KeyHash" ON "ApiKeys" ("KeyHash");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    ALTER TABLE "ApiKeys" DROP COLUMN "Key";
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260321040000_HashApiKeys') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260321040000_HashApiKeys', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322000000_AddApiKeyNameAndAuditFields') THEN
    ALTER TABLE "ApiKeys" ADD "Name" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322000000_AddApiKeyNameAndAuditFields') THEN
    ALTER TABLE "ApiKeys" ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322000000_AddApiKeyNameAndAuditFields') THEN
    ALTER TABLE "ApiKeys" ADD "UpdatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322000000_AddApiKeyNameAndAuditFields') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322000000_AddApiKeyNameAndAuditFields', '10.0.3');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Companies" ADD "PublicUploadToken" text NOT NULL DEFAULT (gen_random_uuid()::text);
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Companies" ADD "QrUploadEnabled" boolean NOT NULL DEFAULT TRUE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Companies" ADD "StripeCustomerId" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE UNIQUE INDEX "IX_Companies_PublicUploadToken" ON "Companies" ("PublicUploadToken");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Users" ADD "TotpSecret" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Users" ADD "TotpBackupCodes" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Users" ADD "RoleId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Users_RoleId" ON "Users" ("RoleId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Users" ADD CONSTRAINT "FK_Users_Roles_RoleId" FOREIGN KEY ("RoleId") REFERENCES "Roles" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Permissions" ADD "Resource" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Permissions" ADD "Action" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE UNIQUE INDEX "UQ_Permissions_Resource_Action" ON "Permissions" ("Resource", "Action");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "RolePermissions" ADD "RoleName" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_RolePermissions_RoleName" ON "RolePermissions" ("RoleName");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Leads" ADD "Name" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Leads" ADD "Email" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Leads" ADD "Phone" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Leads" ADD "CreatedAt" timestamp with time zone NOT NULL DEFAULT (now());
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Leads" ADD "UpdatedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntries" ADD "Description" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntries" ADD "SourceType" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntries" ADD "SourceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntries" ADD "IsPosted" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntries" ADD "PostedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntryLines" ADD "AccountCode" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "JournalEntryLines" ADD "AccountName" text NOT NULL DEFAULT '';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "FiscalYear" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "SequenceNumber" integer NOT NULL DEFAULT 0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "InvoiceType" text NOT NULL DEFAULT 'Normal';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "RectifiedInvoiceId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "IrpfRate" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "IrpfAmount" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "SurchargeAmount" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "LockedAt" timestamp with time zone;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "Hash" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "PreviousHash" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD "JournalEntryId" uuid;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Invoices_CompanyId" ON "Invoices" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE UNIQUE INDEX "UQ_Invoices_CompanyId_Number" ON "Invoices" ("CompanyId", "Number");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Invoices_RectifiedInvoiceId" ON "Invoices" ("RectifiedInvoiceId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Invoices_JournalEntryId" ON "Invoices" ("JournalEntryId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD CONSTRAINT "FK_Invoices_Invoices_RectifiedInvoiceId" FOREIGN KEY ("RectifiedInvoiceId") REFERENCES "Invoices" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Invoices" ADD CONSTRAINT "FK_Invoices_JournalEntries_JournalEntryId" FOREIGN KEY ("JournalEntryId") REFERENCES "JournalEntries" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "InvoiceLines" ADD "TaxAmount" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "InvoiceLines" ADD "SurchargeRate" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "InvoiceLines" ADD "SurchargeAmount" numeric NOT NULL DEFAULT 0.0;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Subscriptions" ADD "StripeSubscriptionId" text;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "Subscriptions" ADD "StripeStatus" text DEFAULT 'inactive';
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Clients_CompanyId" ON "Clients" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_AuditLogs_CompanyId" ON "AuditLogs" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "Suppliers" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "TaxId" text NOT NULL,
        "Email" text NOT NULL,
        "Phone" text NOT NULL,
        "Address" text NOT NULL,
        "BankAccount" text,
        "IsActive" boolean NOT NULL DEFAULT TRUE,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Suppliers" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Suppliers_CompanyId" ON "Suppliers" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "Contacts" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "Name" text NOT NULL,
        "Email" text NOT NULL,
        "Phone" text NOT NULL,
        "Position" text NOT NULL,
        "ClientId" uuid,
        "SupplierId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_Contacts" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_Contacts_Clients_ClientId" FOREIGN KEY ("ClientId") REFERENCES "Clients" ("Id") ON DELETE SET NULL,
        CONSTRAINT "FK_Contacts_Suppliers_SupplierId" FOREIGN KEY ("SupplierId") REFERENCES "Suppliers" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Contacts_CompanyId" ON "Contacts" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Contacts_ClientId" ON "Contacts" ("ClientId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_Contacts_SupplierId" ON "Contacts" ("SupplierId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "ActivityLogs" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "UserId" uuid,
        "EntityType" text NOT NULL,
        "EntityId" uuid NOT NULL,
        "Action" text NOT NULL,
        "Description" text,
        "Timestamp" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_ActivityLogs" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ActivityLogs_CompanyId" ON "ActivityLogs" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "ExpenseDocuments" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ExpenseUploadId" uuid,
        "InvoiceNumber" text,
        "IssueDate" timestamp with time zone,
        "TaxBase" numeric,
        "VATRate" numeric,
        "VATAmount" numeric,
        "IRPFRate" numeric,
        "IRPFAmount" numeric,
        "Total" numeric,
        "OcrRawData" jsonb,
        "OcrConfidence" numeric(5,1),
        "SupplierTaxId" text,
        "SupplierName" text,
        "Status" text NOT NULL DEFAULT 'Draft',
        "IsValidated" boolean NOT NULL DEFAULT FALSE,
        "ValidatedAt" timestamp with time zone,
        "IsLocked" boolean NOT NULL DEFAULT FALSE,
        "HashSignature" text,
        "SupplierId" uuid,
        "AccountingEntryId" uuid,
        "CreatedAt" timestamp with time zone NOT NULL,
        "UpdatedAt" timestamp with time zone,
        CONSTRAINT "PK_ExpenseDocuments" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ExpenseDocuments_CompanyId" ON "ExpenseDocuments" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "ExpenseUploads" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "PublicTokenUsed" text NOT NULL,
        "FilePath" text NOT NULL,
        "FileName" text NOT NULL,
        "ContentType" text NOT NULL,
        "Status" text NOT NULL DEFAULT 'Pending',
        "UploadedAt" timestamp with time zone NOT NULL,
        "Comment" text,
        "ExpenseDocumentId" uuid,
        CONSTRAINT "PK_ExpenseUploads" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpenseUploads_ExpenseDocuments_ExpenseDocumentId" FOREIGN KEY ("ExpenseDocumentId") REFERENCES "ExpenseDocuments" ("Id") ON DELETE SET NULL
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ExpenseUploads_CompanyId" ON "ExpenseUploads" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ExpenseUploads_ExpenseDocumentId" ON "ExpenseUploads" ("ExpenseDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    ALTER TABLE "ExpenseDocuments" ADD CONSTRAINT "FK_ExpenseDocuments_ExpenseUploads_ExpenseUploadId" FOREIGN KEY ("ExpenseUploadId") REFERENCES "ExpenseUploads" ("Id") ON DELETE SET NULL;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ExpenseDocuments_ExpenseUploadId" ON "ExpenseDocuments" ("ExpenseUploadId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "ExpenseDocumentLines" (
        "Id" uuid NOT NULL,
        "ExpenseDocumentId" uuid NOT NULL,
        "Description" text,
        "Quantity" numeric NOT NULL DEFAULT 1.0,
        "UnitPrice" numeric NOT NULL,
        "VATRate" numeric,
        "LineTotal" numeric NOT NULL,
        "ProductId" uuid,
        "SortOrder" integer NOT NULL DEFAULT 0,
        CONSTRAINT "PK_ExpenseDocumentLines" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_ExpenseDocumentLines_ExpenseDocuments_ExpenseDocumentId" FOREIGN KEY ("ExpenseDocumentId") REFERENCES "ExpenseDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_ExpenseDocumentLines_DocumentId" ON "ExpenseDocumentLines" ("ExpenseDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "AccountingEntries" (
        "Id" uuid NOT NULL,
        "CompanyId" uuid NOT NULL,
        "ExpenseDocumentId" uuid NOT NULL,
        "AccountDebit" text NOT NULL,
        "AccountCredit" text NOT NULL,
        "Amount" numeric NOT NULL,
        "PostedAt" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_AccountingEntries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_AccountingEntries_ExpenseDocuments_ExpenseDocumentId" FOREIGN KEY ("ExpenseDocumentId") REFERENCES "ExpenseDocuments" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_AccountingEntries_CompanyId" ON "AccountingEntries" ("CompanyId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_AccountingEntries_ExpenseDocumentId" ON "AccountingEntries" ("ExpenseDocumentId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "RefreshTokens" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "Token" text NOT NULL,
        "ExpiresAt" timestamp with time zone NOT NULL,
        "CreatedAt" timestamp with time zone NOT NULL,
        "IsRevoked" boolean NOT NULL DEFAULT FALSE,
        "ReplacedByToken" text,
        CONSTRAINT "PK_RefreshTokens" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_RefreshTokens_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_RefreshTokens_UserId" ON "RefreshTokens" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE UNIQUE INDEX "UQ_RefreshTokens_Token" ON "RefreshTokens" ("Token");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE TABLE "UserPermissions" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "PermissionId" uuid NOT NULL,
        "IsGranted" boolean NOT NULL DEFAULT TRUE,
        "GrantedBy" text,
        "ExpiresAt" timestamp with time zone,
        CONSTRAINT "PK_UserPermissions" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserPermissions_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE,
        CONSTRAINT "FK_UserPermissions_Permissions_PermissionId" FOREIGN KEY ("PermissionId") REFERENCES "Permissions" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE UNIQUE INDEX "UQ_UserPermissions_User_Permission" ON "UserPermissions" ("UserId", "PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_UserPermissions_UserId" ON "UserPermissions" ("UserId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    CREATE INDEX "IX_UserPermissions_PermissionId" ON "UserPermissions" ("PermissionId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN

                CREATE OR REPLACE FUNCTION prevent_invoice_delete()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'DELETE on Invoices is forbidden by Ley 11/2021 Antifraude';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_no_delete_invoice
                BEFORE DELETE ON public."Invoices"
                FOR EACH ROW EXECUTE FUNCTION prevent_invoice_delete();
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN

                CREATE OR REPLACE FUNCTION prevent_journalentry_delete()
                RETURNS TRIGGER AS $$
                BEGIN
                    RAISE EXCEPTION 'DELETE on JournalEntries is forbidden by Ley 11/2021 Antifraude';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_no_delete_journal
                BEFORE DELETE ON public."JournalEntries"
                FOR EACH ROW EXECUTE FUNCTION prevent_journalentry_delete();
            
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260322010000_CompleteSchema') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260322010000_CompleteSchema', '10.0.3');
    END IF;
END $EF$;
COMMIT;


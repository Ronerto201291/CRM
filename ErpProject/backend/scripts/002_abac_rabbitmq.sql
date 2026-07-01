-- ================================================================
-- ERP SaaS - Sprint 5: RabbitMQ Message Bus + ABAC (Database-First)
-- Run manually on your PostgreSQL instance after 001_schema_extensions.sql
-- RabbitMQ topology is managed by the application at startup (idempotent declares).
-- ================================================================

-- ================================================================
-- PART A: RabbitMQ outbox relay schema extensions
-- ================================================================

-- OutboxMessages already exists from 001_schema_extensions.sql.
-- No structural changes needed — the relay service reuses existing Status/ProcessedAt columns.

-- Add a comment column to OutboxMessages to track relay errors separately from MediatR errors.
-- (Optional: only run if you want separate relay-error tracking in the DB)
ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "RelayError"  TEXT NULL;
ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "RelayedAt"   TIMESTAMPTZ;
ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "Exchange"    TEXT;
ALTER TABLE "OutboxMessages" ADD COLUMN IF NOT EXISTS "RoutingKey"  TEXT;

-- Index for efficient relay polling: pending messages ordered by creation time.
-- Already covered by IX_OutboxMessages_Status and IX_OutboxMessages_CreatedAt from 001.
-- Add a composite index for the relay query pattern: Status + RetryCount + CreatedAt.
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Relay"
    ON "OutboxMessages"("Status", "RetryCount", "CreatedAt")
    WHERE "Status" = 0;  -- OutboxMessageStatus.Pending = 0

-- Partial index for pending-relay queries
CREATE INDEX IF NOT EXISTS "IX_OutboxMessages_Pending_Relay"
    ON "OutboxMessages"("Status", "RelayedAt")
    WHERE "Status" = 0;

-- ================================================================
-- RabbitMQ Topology (managed by application — documented here for ops)
-- ================================================================
-- Exchange : erp.events  (type=topic, durable=true)
-- Queues   :
--   erp.invoice.approved  → binding key: invoice.approved
--   erp.expense.approved  → binding key: expense.approved
--
-- To manually inspect or create via RabbitMQ Management UI:
--   http://localhost:15672 (guest/guest in dev)
--
-- To create via rabbitmqadmin CLI:
--   rabbitmqadmin declare exchange name=erp.events type=topic durable=true
--   rabbitmqadmin declare queue name=erp.invoice.approved durable=true
--   rabbitmqadmin declare queue name=erp.expense.approved durable=true
--   rabbitmqadmin declare binding source=erp.events destination=erp.invoice.approved routing_key=invoice.approved
--   rabbitmqadmin declare binding source=erp.events destination=erp.expense.approved routing_key=expense.approved
-- ================================================================

-- ================================================================
-- PART B: ABAC — Attribute-Based Access Control
-- ================================================================
-- ABAC sits ON TOP of the existing RBAC (ModuleAuthorizationHandler).
-- It provides action-level granularity within a module.
-- Resolution order:
--   1. UserPermissions with IsGranted=false → explicit DENY (wins over everything)
--   2. UserPermissions with IsGranted=true  → explicit GRANT
--   3. RolePermissions (string RoleName)    → role-based grant
--   4. Default                              → denied
-- ================================================================

-- ── Extend existing Permissions table with Resource + Action columns ──────────
ALTER TABLE "Permissions" ADD COLUMN IF NOT EXISTS "Resource" TEXT NOT NULL DEFAULT '';
ALTER TABLE "Permissions" ADD COLUMN IF NOT EXISTS "Action"   TEXT NOT NULL DEFAULT '';

-- Unique constraint: each (Resource, Action) combination must be unique
ALTER TABLE "Permissions"
    DROP CONSTRAINT IF EXISTS "UQ_Permissions_Resource_Action";
ALTER TABLE "Permissions"
    ADD CONSTRAINT "UQ_Permissions_Resource_Action" UNIQUE ("Resource", "Action");

-- ── Extend existing RolePermissions with string-based RoleName ───────────────
-- The composite PK (RoleId, PermissionId) is kept for backward compatibility.
-- The new RoleName column enables cross-tenant role matching by name.
ALTER TABLE "RolePermissions" ADD COLUMN IF NOT EXISTS "RoleName" TEXT NOT NULL DEFAULT '';

CREATE INDEX IF NOT EXISTS "IX_RolePermissions_RoleName"
    ON "RolePermissions"("RoleName");

CREATE INDEX IF NOT EXISTS "IX_RolePermissions_PermissionId"
    ON "RolePermissions"("PermissionId");

-- ── UserPermissions table (new, per-user ABAC overrides) ─────────────────────
CREATE TABLE IF NOT EXISTS "UserPermissions" (
    "Id"           UUID        PRIMARY KEY DEFAULT gen_random_uuid(),
    "UserId"       UUID        NOT NULL REFERENCES "Users"("Id") ON DELETE CASCADE,
    "PermissionId" UUID        NOT NULL REFERENCES "Permissions"("Id") ON DELETE CASCADE,
    "IsGranted"    BOOLEAN     NOT NULL DEFAULT TRUE,
    "GrantedBy"    TEXT,
    "ExpiresAt"    TIMESTAMPTZ,
    CONSTRAINT "UQ_UserPermissions_User_Permission" UNIQUE ("UserId", "PermissionId")
);

CREATE INDEX IF NOT EXISTS "IX_UserPermissions_UserId"
    ON "UserPermissions"("UserId");

CREATE INDEX IF NOT EXISTS "IX_UserPermissions_PermissionId"
    ON "UserPermissions"("PermissionId");

-- ── Seed ABAC Permissions ─────────────────────────────────────────────────────
INSERT INTO "Permissions" ("Resource", "Action", "Description", "Code") VALUES
  ('Invoice',        'Create',      'Crear facturas',                            'Invoice:Create'),
  ('Invoice',        'Read',        'Ver facturas',                              'Invoice:Read'),
  ('Invoice',        'Update',      'Editar facturas en borrador',               'Invoice:Update'),
  ('Invoice',        'Delete',      'Anular facturas (rectificativa)',            'Invoice:Delete'),
  ('Invoice',        'Lock',        'Bloquear/emitir facturas',                  'Invoice:Lock'),
  ('Invoice',        'Approve',     'Aprobar facturas',                          'Invoice:Approve'),
  ('Expense',        'Create',      'Crear gastos',                              'Expense:Create'),
  ('Expense',        'Read',        'Ver gastos',                                'Expense:Read'),
  ('Expense',        'Update',      'Editar gastos en borrador',                 'Expense:Update'),
  ('Expense',        'Approve',     'Aprobar gastos y generar asiento',          'Expense:Approve'),
  ('Client',         'Create',      'Crear clientes',                            'Client:Create'),
  ('Client',         'Read',        'Ver clientes',                              'Client:Read'),
  ('Client',         'Update',      'Editar clientes',                           'Client:Update'),
  ('Client',         'Delete',      'Eliminar clientes',                         'Client:Delete'),
  ('Accounting',     'Read',        'Ver contabilidad y reportes',               'Accounting:Read'),
  ('Accounting',     'ManualEntry', 'Crear asientos manuales',                  'Accounting:ManualEntry'),
  ('UserManagement', 'Read',        'Ver usuarios',                              'UserManagement:Read'),
  ('UserManagement', 'Write',       'Crear y editar usuarios',                   'UserManagement:Write'),
  ('UserManagement', 'Delete',      'Eliminar usuarios',                         'UserManagement:Delete'),
  ('Inventory',      'Read',        'Ver inventario',                            'Inventory:Read'),
  ('Inventory',      'Write',       'Gestionar stock y productos',               'Inventory:Write'),
  ('Report',         'Export',      'Exportar datos (SII, Excel, PDF)',           'Report:Export')
ON CONFLICT ("Resource", "Action") DO NOTHING;

-- ── Seed RolePermissions (string RoleName — ABAC approach) ───────────────────
-- Uses a null/zero RoleId UUID since these rows are resolved by RoleName, not FK.

-- Admin: all permissions
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "RoleName")
SELECT '00000000-0000-0000-0000-000000000000'::uuid, p."Id", 'Admin'
FROM "Permissions" p
WHERE p."Resource" != '' AND p."Action" != ''
ON CONFLICT DO NOTHING;

-- Manager: all except UserManagement:Delete and Accounting:ManualEntry
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "RoleName")
SELECT '00000000-0000-0000-0000-000000000000'::uuid, p."Id", 'Manager'
FROM "Permissions" p
WHERE p."Resource" != '' AND p."Action" != ''
  AND NOT (p."Resource" = 'UserManagement' AND p."Action" = 'Delete')
  AND NOT (p."Resource" = 'Accounting'     AND p."Action" = 'ManualEntry')
ON CONFLICT DO NOTHING;

-- Contable: Accounting (all), Report:Export, Invoice:Read, Expense:Read
INSERT INTO "RolePermissions" ("RoleId", "PermissionId", "RoleName")
SELECT '00000000-0000-0000-0000-000000000000'::uuid, p."Id", 'Contable'
FROM "Permissions" p
WHERE p."Resource" != '' AND p."Action" != ''
  AND (
        p."Resource" IN ('Accounting', 'Report')
     OR (p."Resource" IN ('Invoice', 'Expense') AND p."Action" = 'Read')
  )
ON CONFLICT DO NOTHING;

-- ── Security trigger: prevent DELETE on Permissions ───────────────────────────
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

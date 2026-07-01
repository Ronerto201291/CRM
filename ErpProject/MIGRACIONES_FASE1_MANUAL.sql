-- ============================================
-- FASE 1 MIGRATIONS - Manual SQL Execution
-- ============================================
-- Database: erp_main_db
-- User: postgres
-- Password: postgres

-- Create schemas
CREATE SCHEMA IF NOT EXISTS purchasing;
CREATE SCHEMA IF NOT EXISTS sales;
CREATE SCHEMA IF NOT EXISTS inventory;

-- ============================================
-- PURCHASING SCHEMA
-- ============================================

CREATE TABLE purchasing."PurchaseOrders" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "VendorId" uuid NOT NULL,
    "OrderDate" timestamp with time zone NOT NULL,
    "DueDate" timestamp with time zone,
    "Status" text NOT NULL DEFAULT 'Draft',
    "TotalAmount" numeric(18,4) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_PurchaseOrders" PRIMARY KEY ("Id")
);

CREATE TABLE purchasing."GoodsReceipts" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "PurchaseOrderId" uuid NOT NULL,
    "ReceiptDate" timestamp with time zone NOT NULL,
    "Status" text NOT NULL DEFAULT 'Pending',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_GoodsReceipts" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_GoodsReceipts_PurchaseOrders" FOREIGN KEY ("PurchaseOrderId") REFERENCES purchasing."PurchaseOrders"("Id") ON DELETE CASCADE
);

CREATE TABLE purchasing."SupplierInvoices" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "VendorId" uuid NOT NULL,
    "GoodsReceiptId" uuid,
    "InvoiceNumber" text NOT NULL,
    "InvoiceDate" timestamp with time zone NOT NULL,
    "TotalAmount" numeric(18,4) NOT NULL,
    "Status" text NOT NULL DEFAULT 'Draft',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_SupplierInvoices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SupplierInvoices_GoodsReceipts" FOREIGN KEY ("GoodsReceiptId") REFERENCES purchasing."GoodsReceipts"("Id")
);

CREATE INDEX "IX_PurchaseOrders_CompanyId" ON purchasing."PurchaseOrders"("CompanyId");
CREATE INDEX "IX_GoodsReceipts_CompanyId" ON purchasing."GoodsReceipts"("CompanyId");
CREATE INDEX "IX_SupplierInvoices_CompanyId" ON purchasing."SupplierInvoices"("CompanyId");

-- ============================================
-- SALES SCHEMA
-- ============================================

CREATE TABLE sales."SalesOrders" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "OrderDate" timestamp with time zone NOT NULL,
    "DueDate" timestamp with time zone,
    "Status" text NOT NULL DEFAULT 'Draft',
    "TotalAmount" numeric(18,4) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_SalesOrders" PRIMARY KEY ("Id")
);

CREATE TABLE sales."DeliveryNotes" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "SalesOrderId" uuid NOT NULL,
    "DeliveryDate" timestamp with time zone NOT NULL,
    "Status" text NOT NULL DEFAULT 'Pending',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_DeliveryNotes" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_DeliveryNotes_SalesOrders" FOREIGN KEY ("SalesOrderId") REFERENCES sales."SalesOrders"("Id") ON DELETE CASCADE
);

CREATE TABLE sales."CustomerInvoices" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ClientId" uuid NOT NULL,
    "DeliveryNoteId" uuid,
    "InvoiceNumber" text NOT NULL,
    "InvoiceDate" timestamp with time zone NOT NULL,
    "TotalAmount" numeric(18,4) NOT NULL,
    "Status" text NOT NULL DEFAULT 'Draft',
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_CustomerInvoices" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_CustomerInvoices_DeliveryNotes" FOREIGN KEY ("DeliveryNoteId") REFERENCES sales."DeliveryNotes"("Id")
);

CREATE INDEX "IX_SalesOrders_CompanyId" ON sales."SalesOrders"("CompanyId");
CREATE INDEX "IX_DeliveryNotes_CompanyId" ON sales."DeliveryNotes"("CompanyId");
CREATE INDEX "IX_CustomerInvoices_CompanyId" ON sales."CustomerInvoices"("CompanyId");

-- ============================================
-- INVENTORY SCHEMA
-- ============================================

CREATE TABLE inventory."Products" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Name" text NOT NULL,
    "SKU" text NOT NULL,
    "UnitPrice" numeric(18,4) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Products" PRIMARY KEY ("Id")
);

CREATE TABLE inventory."Warehouses" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "Name" text NOT NULL,
    "Location" text,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Warehouses" PRIMARY KEY ("Id")
);

CREATE TABLE inventory."Stock" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Stock" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_Stock_Products" FOREIGN KEY ("ProductId") REFERENCES inventory."Products"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_Stock_Warehouses" FOREIGN KEY ("WarehouseId") REFERENCES inventory."Warehouses"("Id") ON DELETE CASCADE,
    CONSTRAINT "CK_Stock_Quantity_NonNegative" CHECK ("Quantity" >= 0)
);

CREATE TABLE inventory."StockMovements" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "WarehouseId" uuid NOT NULL,
    "MovementType" text NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "MovementDate" timestamp with time zone NOT NULL,
    "ReferenceId" uuid,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_StockMovements" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_StockMovements_Products" FOREIGN KEY ("ProductId") REFERENCES inventory."Products"("Id") ON DELETE CASCADE,
    CONSTRAINT "FK_StockMovements_Warehouses" FOREIGN KEY ("WarehouseId") REFERENCES inventory."Warehouses"("Id") ON DELETE CASCADE
);

CREATE TABLE inventory."Lots" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "LotNumber" text NOT NULL,
    "ExpirationDate" timestamp with time zone NOT NULL,
    "Quantity" numeric(18,4) NOT NULL,
    "UnitCost" numeric(18,4) NOT NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_Lots" PRIMARY KEY ("Id")
);

CREATE TABLE inventory."SerialNumbers" (
    "Id" uuid NOT NULL,
    "CompanyId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "LotId" uuid,
    "Serial" text NOT NULL,
    "Status" text NOT NULL DEFAULT 'Available',
    "SoldDate" timestamp with time zone,
    "CreatedAt" timestamp with time zone NOT NULL,
    "UpdatedAt" timestamp with time zone,
    CONSTRAINT "PK_SerialNumbers" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_SerialNumbers_Lots" FOREIGN KEY ("LotId") REFERENCES inventory."Lots"("Id")
);

CREATE INDEX "IX_Products_CompanyId" ON inventory."Products"("CompanyId");
CREATE INDEX "IX_Warehouses_CompanyId" ON inventory."Warehouses"("CompanyId");
CREATE INDEX "IX_Stock_CompanyId" ON inventory."Stock"("CompanyId");
CREATE INDEX "IX_StockMovements_CompanyId_MovementType" ON inventory."StockMovements"("CompanyId", "MovementType");
CREATE INDEX "IX_Lots_CompanyId" ON inventory."Lots"("CompanyId");
CREATE INDEX "IX_SerialNumbers_CompanyId" ON inventory."SerialNumbers"("CompanyId");

-- ============================================
-- DONE - All migrations applied
-- ============================================
COMMIT;

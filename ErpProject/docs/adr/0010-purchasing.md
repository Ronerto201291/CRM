# ADR-0010: Purchasing

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Purchasing cubre el ciclo de compras a proveedores de un ERP para
España: emisión de pedidos de compra (`PurchaseOrder`), recepción de
mercancía (`GoodsReceipt`) y registro de facturas de proveedor
(`SupplierInvoice`), con un control de "three-way match" (pedido ↔ recepción
↔ factura) antes de aceptar una factura.

**Corregido (jul 2026):** `PurchaseOrder.SupplierId` y `SupplierInvoice.SupplierId`
(FK a `crm.Suppliers`), validación cross-módulo vía `ISupplierInfoService`.

## Decisión
### Backend
- `PurchaseOrdersController` — CQRS vía `IMediator`; `SupplierId` obligatorio en create/update.
- `InvoicesController` — `CreateSupplierInvoiceCommand` exige `SupplierId`;
  validación CRM vía `ISupplierInfoService`; si el pedido tiene `SupplierId`,
  la factura debe coincidir.
- `GetAllSupplierInvoicesQuery` / `GetSupplierInvoiceQuery` devuelven `supplierId`/`supplierName`.

Migración `20260706120000_AddSupplierInvoiceSupplierFk`: columna nullable
`SupplierId` + FK `purchasing.SupplierInvoices` → `crm.Suppliers`.

### Frontend
- `invoices/new/page.tsx`: selector proveedor CRM + Zod (`supplierId` en schema).
- Listados/detalle muestran `supplierName` resuelto desde backend.

### Modelo de datos
- `PurchaseOrder`: `SupplierId?` FK CRM.
- `SupplierInvoice`: `SupplierId?` FK CRM (jul 2026 — ya no solo derivable del pedido).

## Relación con otros módulos
- **ADR-0004 (CRM):** `ISupplierInfoService` (contrato Application, impl Crm.Infrastructure).
- **ADR-0006 (Accounting):** `SupplierInvoiceCreatedEvent` → asiento 600/472/410 (#69 ✅).
- **ADR-0008 (Inventory):** recepción incrementa stock (#20 ✅).

## Evaluación de calidad arquitectónica
- **Controllers delgados:** ✅ (#11).
- **SupplierId en factura:** ✅ (jul 2026) — mismo patrón que pedidos.
- **Three-way match:** preservado en `ThreeWayMatchValidator`.

## Consecuencias
- Reporting cross-módulo por proveedor disponible en pedidos **y facturas**.
- Frontend alineado: selector CRM en alta de factura; Zod obligatorio.
- Tests: `CreateSupplierInvoiceHandlerTests` (supplier missing, mismatch PO, OK).

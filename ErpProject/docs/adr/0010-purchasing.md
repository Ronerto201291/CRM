# ADR-0010: Purchasing

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Purchasing cubre el ciclo de compras a proveedores de un ERP para
España: emisión de pedidos de compra (`PurchaseOrder`), recepción de
mercancía (`GoodsReceipt`) y registro de facturas de proveedor
(`SupplierInvoice`), con un control de "three-way match" (pedido ↔ recepción
↔ factura) antes de aceptar una factura. El código vive en
`backend/Modules/Purchasing/` (backend) y en
`frontend/src/app/purchasing/` (frontend, Next.js 15 App Router).

A diferencia de otros módulos del monolito modular, la implementación de
Purchasing sigue siendo parcial: **sí existe** un flujo de aprobación de
pedidos de compra (ver más abajo, ADR-0018 #30) y **sí existe** integración
activa con Inventario (recepción de mercancía → stock, ADR-0018 #20) y
**sí existe** integración con Contabilidad vía eventos (factura de proveedor
validada → asiento 600/472/410, ADR-0018 #69 ✅), pero **no hay** vínculo
persistido con el proveedor (`Supplier`) de CRM. Esta ADR documenta el estado
"as-built" tal cual existe hoy, incluyendo esas carencias, para que cualquier
trabajo futuro sobre el módulo parta de hechos verificados y no de
suposiciones sobre cómo "debería" funcionar un ERP de compras.

## Decisión
### Backend
La estructura de carpetas sigue `Api/Application/Domain/Infrastructure` con
**cuatro `.csproj` separados** (ADR-0018 #12) — frontera real de compilador
entre capas, alineado con el resto de módulos salvo la migración física que
vivió temporalmente en la raíz del módulo (#19g corregido: ahora bajo
`Infrastructure/Migrations/`).

Controllers (`backend/Modules/Purchasing/Api/Controllers/`):
- `PurchaseOrdersController` — ruta `api/v{version:apiVersion}/purchasing/orders`.
  `GET` (lista), `GET {id}`, `POST`, `PUT {id}`, `DELETE {id}`. **Corregido
  (backlog #11):** controller delgado vía `IMediator`
  (`Application/Features/Orders/PurchaseOrderHandlers.cs`); filtra por
  `CompanyId` del tenant. No lleva `[Authorize]`. **Flujo de aprobación
  (ADR-0018 #30, añadido jul 2026 — sección ausente hasta esta
  actualización):** `POST {id}/submit-for-approval`, `POST {id}/approve` y
  `POST {id}/reject` despachan `SubmitPurchaseOrderForApprovalCommand`/
  `ApprovePurchaseOrderCommand`/`RejectPurchaseOrderCommand` vía `IMediator`.
  `PurchaseOrder.Status` transiciona `Draft`→`PendingApproval`→
  `Approved`/`Rejected`; el umbral que exige aprobación es
  `Company.ApprovalThresholdAmount` (configurable por tenant). Frontend real:
  `orders/[id]/OrderDetailClient.tsx` hace `POST` real a estas tres rutas y
  muestra el estado con badges.
- `ReceiptsController` — ruta `api/v{version:apiVersion}/purchasing/receipts`.
  `GET` listado paginado (`GetAllGoodsReceiptsQuery`), `POST` →
  `CreateGoodsReceiptCommand`, `GET {id}` → `GetGoodsReceiptQuery` con líneas
  reales (ADR-0018 #68 ✅).
- `InvoicesController` — ruta `api/v{version:apiVersion}/purchasing/invoices`.
  `GET` listado paginado (`GetAllSupplierInvoicesQuery`), `POST` →
  `CreateSupplierInvoiceCommand`, `GET {id}` → `GetSupplierInvoiceQuery` (#68 ✅).

CQRS (`backend/Modules/Purchasing/Application/Features/`): solo existen dos
casos de uso implementados con MediatR, `Receipts/Commands/CreateGoodsReceiptCommand`
con su `Receipts/Handlers/CreateGoodsReceiptHandler`, e
`Invoices/Commands/CreateSupplierInvoiceCommand` con su
`Invoices/Handlers/CreateSupplierInvoiceHandler`, más queries/commands de
pedidos en `Orders/PurchaseOrderHandlers.cs` (backlog #11). No hay otros
queries MediatR de lectura ni validadores FluentValidation; la única
validación de negocio no trivial es
`Application/Validators/ThreeWayMatchValidator.cs`.

`CreateGoodsReceiptHandler` valida que la `PurchaseOrderLine` referenciada
exista, pertenezca al pedido indicado, y que `QuantityReceived` no supere la
cantidad pedida (`pol.Quantity`); si algo falla lanza
`InvalidOperationException`.

`CreateSupplierInvoiceHandler` resuelve el `PurchaseOrder`, obtiene la
tolerancia de matching configurable por tenant
(`Company.MatchingToleranceAmount`, en `backend/Erp.Domain/Entities/Core/Company.cs`,
por defecto `0m`), construye la `SupplierInvoice` con sus líneas y ejecuta
`ThreeWayMatchValidator.ValidateInvoiceAgainstOrderAndReceipt(invoice, _context, tolerance)`
**antes** de añadir la factura al contexto y hacer `SaveChangesAsync`. Tras
persistir, publica `SupplierInvoiceCreatedEvent` vía `IPublisher` (ADR-0018 #69).
Si el
validador lanza excepción, la factura no llega a persistirse: es un bloqueo
duro, no un aviso ni un flag que se guarde en la entidad (la factura no
tiene ningún campo de estado de matching).

`ThreeWayMatchValidator.ValidateInvoiceAgainstOrderAndReceipt` (estático),
por cada línea de factura:
1. Busca la `PurchaseOrderLine` por `Id`; si no existe, lanza excepción.
2. Suma `QuantityReceived` de todas las `GoodsReceiptLine` asociadas a esa
   línea de pedido (histórico completo de recepciones, no una recepción
   concreta).
3. Si `invoiceLine.Quantity > receivedQty`, lanza
   `"Invoice quantity exceeds received quantity"`.
4. Compara el importe de la línea de factura (`Quantity * UnitPrice`) contra
   el importe de la línea de pedido (`pol.Quantity * pol.UnitPrice`); si la
   diferencia absoluta supera `toleranceAmount`, lanza
   `"Invoice line amount differs from order beyond tolerance"`.

El validador no compara contra el precio unitario de la recepción
(`GoodsReceiptLine.UnitPrice` solo se usa indirectamente al no leerse en el
cálculo de importe), y no distingue "coincide con tolerancia" de "no
coincide": cualquier fallo aborta la operación completa vía excepción no
capturada, que el pipeline HTTP estándar convertirá en un error 4xx/5xx sin
un código de negocio específico.

`IPurchasingDbContext` (`Application/Interfaces/IPurchasingDbContext.cs`)
expone los `DbSet` de las seis entidades y `Database`/`SaveChangesAsync`, y
lo implementa `PurchasingDbContext`
(`Infrastructure/Data/PurchasingDbContext.cs`), que hereda de
`ModuleDbContextBase` (timestamps automáticos de `AuditableEntity` y
autorrelleno de `CompanyId` desde `ITenantContext` en `SaveChangesAsync`).
Usa el esquema PostgreSQL `purchasing` y aplica `HasQueryFilter` por
`CompanyId == TenantContext.TenantId` en `PurchaseOrder`, `GoodsReceipt` y
`SupplierInvoice`.

### Frontend
Rutas existentes bajo `frontend/src/app/purchasing/` (no existe una página
raíz `purchasing/page.tsx`):
- `orders/page.tsx` — listado de pedidos con filtro por estado y mini-KPIs.
- `orders/new/page.tsx` — formulario de alta de pedido, con selector de
  proveedor cargado desde `/api/proxy/suppliers` (CRM) y líneas con
  cantidad/precio/IVA.
- `orders/[id]/page.tsx` — detalle de un pedido, con acciones "Registrar
  Recepción" y "Crear Factura" que navegan a los formularios
  correspondientes precargando `purchaseOrderId`, y un botón "Cancelar" que
  llama a `PATCH /purchasing/orders/{id}/cancel`.
- `receipts/page.tsx` y `receipts/new/page.tsx` — listado (datos reales vía
  `GET /api/v1/purchasing/receipts`) y alta con líneas del pedido aprobado
  (`purchaseOrderLineId`, `productId`, `unitPrice`; ADR-0018 #67 ✅).
- `invoices/page.tsx` y `invoices/new/page.tsx` — listado y alta contra
  `purchaseOrderId` con líneas del pedido (#67 ✅).

Todas las páginas son componentes `"use client"` que llaman al backend a
través de `frontend/src/app/api/proxy/[...path]/route.ts`, un proxy
genérico que reenvía a `${API_URL}/api/${path}` añadiendo el token y el
`X-Tenant-Id` desde cookies. Las páginas de listado llaman a
`/api/proxy/purchasing/{orders|invoices|receipts}` (sin prefijo de
versión), mientras que las páginas de alta/detalle llaman a
`/api/proxy/v1/purchasing/...` (con `v1`). Solo la segunda forma coincide
con la ruta real de los controllers (`api/v{version:apiVersion}/...`).

### Modelo de datos
Entidades (todas `AuditableEntity`: `Id`, `CreatedAt`, `UpdatedAt`, más
`CompanyId`):
- `PurchaseOrder` (`Number`, `OrderDate`) — 1 a N `PurchaseOrderLine`
  (`ProductId?`, `Quantity`, `UnitPrice`), borrado en cascada.
- `GoodsReceipt` (`PurchaseOrderId`, `Number`, `ReceiptDate`) — 1 a N
  `GoodsReceiptLine` (`PurchaseOrderLineId`, `ProductId?`,
  `QuantityReceived`, `UnitPrice`), borrado en cascada de las líneas; el FK
  `GoodsReceipt → PurchaseOrder` es `Restrict`.
- `SupplierInvoice` (`PurchaseOrderId`, `Number`, `InvoiceDate`,
  `TotalAmount`) — 1 a N `SupplierInvoiceLine` (`PurchaseOrderLineId`,
  `ProductId?`, `Quantity`, `UnitPrice`, `LineTotal` calculado), borrado en
  cascada; el FK a `PurchaseOrder` es `Restrict`.

Ninguna de las tres entidades cabecera tiene `SupplierId`, campo de estado
(`Status`), ni desglose de impuestos (`taxRate`/`taxAmount`): esos campos
existen únicamente en las interfaces TypeScript del frontend, no en el
esquema real (confirmado en la migración
`Migrations/20260410235151_InitialPurchasing.cs`, que crea las tablas
`PurchaseOrders`, `GoodsReceipts`, `PurchaseOrderLines`, `SupplierInvoices`,
`GoodsReceiptLines`, `SupplierInvoiceLines` en el esquema `purchasing` con
exactamente esas columnas). `PurchaseOrder` tiene un índice único
`(CompanyId, Number)`.

### Flujo end-to-end representativo
1. El usuario crea un pedido desde `orders/new/page.tsx`, que envía
   `POST /api/proxy/v1/purchasing/orders` → `PurchaseOrdersController.Create`
   → `CreatePurchaseOrderCommand` vía `IMediator` (backlog #11).
2. Desde `orders/[id]/page.tsx`, el usuario pulsa "Registrar Recepción",
   navega a `receipts/new/page.tsx` y envía
   `POST /api/proxy/v1/purchasing/receipts` con el `purchaseOrderId` y
   líneas recibidas → `ReceiptsController.Create` → MediatR →
   `CreateGoodsReceiptHandler`, que valida cantidades contra la línea de
   pedido y persiste `GoodsReceipt`/`GoodsReceiptLine`.
3. El usuario pulsa "Crear Factura", navega a `invoices/new/page.tsx` y
   envía `POST /api/proxy/v1/purchasing/invoices` →
   `InvoicesController.Create` → MediatR → `CreateSupplierInvoiceHandler`,
   que resuelve la tolerancia (`Company.MatchingToleranceAmount`) y ejecuta
   `ThreeWayMatchValidator.ValidateInvoiceAgainstOrderAndReceipt` antes de
   guardar. Si alguna línea factura más cantidad de la recibida, o su
   importe se desvía del importe de la línea de pedido por encima de la
   tolerancia, la operación se aborta con `InvalidOperationException` y no
   se crea la factura.
4. La recepción dispara `GoodsReceiptCreatedEvent` → Inventory incrementa
   stock (ADR-0018 #20). La factura dispara `SupplierInvoiceCreatedEvent` →
   Accounting crea asiento automático 600/472/410 (ADR-0018 #69 ✅). No hay
   obligación de pago en Treasury al crear la factura de proveedor.

## Relación con otros módulos
- **ADR-0001 (patrón compartido):** ✅ cuatro proyectos por capa (#12);
  migraciones bajo `Infrastructure/Migrations/` (#19g).
- **ADR-0002 (multi-tenant)**: `PurchasingDbContext` sigue el mismo patrón
  de `HasQueryFilter` por `CompanyId` y autorrelleno de `CompanyId` vía
  `ITenantContext` que el resto de módulos.
- **ADR-0004 (CRM)**: CRM es dueño de la entidad `Supplier`
  (`backend/Modules/Crm/Domain/Entities/Supplier.cs`, con `CIF/NIF`,
  `SuppliersController` propio). Purchasing **no** tiene ninguna entidad ni
  columna `SupplierId` en `PurchaseOrder` ni en `SupplierInvoice` (verificado
  en las entidades y en la migración): el frontend consulta
  `/api/proxy/suppliers` (CRM) para poblar los desplegables, pero el
  `supplierId` elegido nunca se envía como campo aceptado por
  `CreatePoDto` ni por `CreateSupplierInvoiceCommand`, así que no queda
  persistido en Purchasing. No hay, por tanto, vínculo real en base de
  datos entre un pedido/factura de compra y el proveedor de CRM.
- **ADR-0006 (Accounting)**: no se ha encontrado ningún código en
  `backend/Modules/Accounting/` que referencie `SupplierInvoice` ni
  `Purchasing`, ni ningún evento/notificación MediatR o llamada directa
  desde Purchasing hacia Accounting. La contabilización de la factura de
  proveedor no está implementada.
- **ADR-0008 (Inventory)**: ✅ Corregido (ADR-0018 #20) — `CreateGoodsReceiptHandler`
  publica `GoodsReceiptCreatedEvent` vía MediatR; `GoodsReceiptInventoryHandler`
  (Inventory) incrementa stock en el almacén activo de la empresa, con
  idempotencia por `ReferenceType=GoodsReceipt`.

## Evaluación de calidad arquitectónica
> Metodología completa en `ADR-0018`.

- **Estructura:** ✅ cuatro `.csproj` con frontera de compilador (#12).
- **Controllers delgados:** ✅ `PurchaseOrdersController` vía `IMediator` (#11).
- **N+1:** ✅ corregido en `CreateGoodsReceiptHandler` (#7).
- **Pendiente:** `SupplierId` no persistido en `PurchaseOrder` (solo texto suelto
  vía CRM); campos de dominio que el frontend modela de más (`supplierName`,
  estados `Paid`/`Draft`, etc.).
- **✅ Corregido (jul 2026, #67–#69):** formularios `receipts/new` e `invoices/new`
  cargan líneas del pedido con `purchaseOrderLineId`/`productId`; listados y
  `GET {id}` reales (#68); `SupplierInvoiceCreatedEvent` → asiento contable (#69).

## Buenas prácticas aplicables
- El "three-way match" en `ThreeWayMatchValidator` es el patrón de control
  a preservar si se añaden nuevos flujos de facturación: validar
  cantidades acumuladas recibidas contra lo facturado, y comparar importes
  con una tolerancia configurable por compañía (`Company.MatchingToleranceAmount`)
  en vez de una constante global.
- Los handlers (`CreateGoodsReceiptHandler`, `CreateSupplierInvoiceHandler`)
  validan pertenencia de la línea al pedido y límites de cantidad antes de
  persistir, lanzando `InvalidOperationException` con mensaje descriptivo;
  seguir ese mismo estilo de validación temprana dentro del handler en
  lugar de delegarla a la base de datos.
- Al extender endpoints de pedidos, seguir el patrón CQRS ya usado en
  `Application/Features/Orders/PurchaseOrderHandlers.cs` (#11).
- Revisar y homogeneizar `[Authorize]`: solo `ReceiptsController` lo declara
  hoy; `PurchaseOrdersController` e `InvoicesController` no.

## Consecuencias
- **Estructura:** ✅ alineada con ADR-0001 (#12, #19g).
- **Endpoints de lectura** ✅ (#68): listados y detalle reales en
  `ReceiptsController`/`InvoicesController`. Siguen pendientes campos de
  dominio que el frontend aún modela de más (`supplierName`, estados
  `Paid`/`Draft`, etc.).
- **Desajuste frontend/backend (cosmético):** el frontend modela estados y
  campos que el backend no persiste (`supplierName` derivado, estados
  `Paid`/`Draft` en facturas, etc.) — no bloquea el flujo principal.
- **Proveedor no vinculado**: al no persistirse `SupplierId` en Purchasing,
  no hay FK a CRM; reporting cross-módulo requiere añadir la columna.
- **Integración Inventory** ✅ (#20): recepción incrementa stock.
  **Integración Accounting** ✅ (#69): factura validada publica
  `SupplierInvoiceCreatedEvent` → asiento 600/472/410. Pendiente: obligación
  de pago automática en Treasury.
- El validador de three-way match no persiste su resultado (no hay estado
  de "conciliado"/"con discrepancias" en `SupplierInvoice`): un fallo
  bloquea la creación por completo, sin posibilidad de guardar la factura
  como pendiente de revisión.

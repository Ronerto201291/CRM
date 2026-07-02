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

A diferencia de otros módulos del monolito modular, la implementación actual
de Purchasing es deliberadamente mínima: no hay todavía flujo de aprobación
de facturas, ni integración activa con Inventario o Contabilidad, ni vínculo
persistido con el proveedor (`Supplier`) de CRM. Esta ADR documenta el
estado "as-built" tal cual existe hoy, incluyendo esas carencias, para que
cualquier trabajo futuro sobre el módulo parta de hechos verificados y no de
suposiciones sobre cómo "debería" funcionar un ERP de compras.

## Decisión
### Backend
La estructura de carpetas seguida es `Api/Controllers`, `Application`,
`Domain/Entities`, `Infrastructure/Data`, igual que el resto de módulos
según el patrón compartido, pero con dos desviaciones físicas relevantes
(ver "Relación con otros módulos" y "Consecuencias").

Controllers (`backend/Modules/Purchasing/Api/Controllers/`):
- `PurchaseOrdersController` — ruta `api/v{version:apiVersion}/purchasing/orders`.
  `GET` (lista, con `Include(Lines)`), `GET {id}`, `POST`, `PUT {id}`,
  `DELETE {id}`. No usa MediatR: opera directamente sobre
  `IPurchasingDbContext` desde el propio controller. No lleva `[Authorize]`.
- `ReceiptsController` — ruta `api/v{version:apiVersion}/purchasing/receipts`,
  con `[Authorize]`. `POST` despacha `CreateGoodsReceiptCommand` vía
  `IMediator`. `GET {id}` es un stub que solo devuelve `{ id }`, sin datos
  reales de la recepción.
- `InvoicesController` — ruta `api/v{version:apiVersion}/purchasing/invoices`,
  sin `[Authorize]`. `POST` despacha `CreateSupplierInvoiceCommand` vía
  `IMediator`. `GET {id}` es igualmente un stub `{ id }`.

CQRS (`backend/Modules/Purchasing/Application/Features/`): solo existen dos
casos de uso implementados con MediatR, `Receipts/Commands/CreateGoodsReceiptCommand`
con su `Receipts/Handlers/CreateGoodsReceiptHandler`, e
`Invoices/Commands/CreateSupplierInvoiceCommand` con su
`Invoices/Handlers/CreateSupplierInvoiceHandler`. No hay queries MediatR (las
lecturas se hacen directamente en `PurchaseOrdersController`) ni validadores
FluentValidation; la única validación de negocio no trivial es
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
**antes** de añadir la factura al contexto y hacer `SaveChangesAsync`. Si el
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
- `receipts/page.tsx` y `receipts/new/page.tsx` — listado y alta de
  recepciones, seleccionando un pedido existente.
- `invoices/page.tsx` y `invoices/new/page.tsx` — listado y alta de
  facturas de proveedor.

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
   `POST /api/proxy/v1/purchasing/orders` → `PurchaseOrdersController.Create`,
   el cual construye `PurchaseOrder` + `PurchaseOrderLine` directamente y
   llama a `_context.SaveChangesAsync`. (El `supplierId` que el formulario
   recoge no forma parte de `CreatePoDto`, por lo que se descarta.)
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
4. No existe ningún paso posterior de aprobación, contabilización o
   actualización de stock: el flujo termina en la persistencia de la
   factura de proveedor.

## Relación con otros módulos
- **ADR-0001 (patrón compartido de módulos)**: Purchasing reutiliza
  `AuditableEntity`, `ModuleDbContextBase`, MediatR y versionado de API
  (`Asp.Versioning`) como el resto de módulos, pero con dos desviaciones de
  carpeta verificadas: (a) un único `.csproj` en la raíz del módulo,
  `backend/Modules/Purchasing/Erp.Modules.Purchasing.Infrastructure.csproj`,
  en vez de un `.csproj` por capa dentro de `Api/`, `Application/`,
  `Domain/` e `Infrastructure/` (compárese con Inventory, que tiene cuatro
  `.csproj`: `Domain/Erp.Modules.Inventory.Domain.csproj`,
  `API/Erp.Modules.Inventory.Api.csproj`,
  `Application/Erp.Modules.Inventory.Application.csproj` e
  `Infrastructure/Erp.Modules.Inventory.Infrastructure.csproj`); y (b) la
  carpeta de migraciones vive en `backend/Modules/Purchasing/Migrations/`
  (raíz del módulo) en vez de `backend/Modules/Purchasing/Infrastructure/Migrations/`
  (compárese con `backend/Modules/Inventory/Infrastructure/Migrations/`).
  El namespace de la migración generada sigue siendo
  `Erp.Modules.Purchasing.Infrastructure.Migrations` pese a residir
  físicamente fuera de `Infrastructure/`.
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
- **ADR-0008 (Inventory)**: no se ha encontrado ningún código en
  `backend/Modules/Inventory/` que referencie `GoodsReceipt` ni
  `Purchasing`. `CreateGoodsReceiptHandler` no publica ningún evento de
  dominio ni notificación MediatR (no hay `INotification`, `DomainEvent` ni
  patrón Outbox en todo `backend/Modules/Purchasing/`). Registrar una
  recepción de mercancía no actualiza el stock de Inventario.

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
- Al extender `PurchaseOrdersController` (que hoy accede a
  `IPurchasingDbContext` directamente desde el controller), lo coherente
  con el resto del módulo (Receipts, Invoices) y con el patrón CQRS del
  monolito sería migrarlo a comandos/queries MediatR en vez de seguir
  añadiendo lógica en el controller.
- Revisar y homogeneizar `[Authorize]`: solo `ReceiptsController` lo declara
  hoy; `PurchaseOrdersController` e `InvoicesController` no.

## Consecuencias
- **Desviación de estructura de carpetas (confirmada)**: un solo `.csproj`
  de módulo en la raíz (`Erp.Modules.Purchasing.Infrastructure.csproj`) en
  vez de un ensamblado por capa, y `Migrations/` en la raíz del módulo en
  vez de bajo `Infrastructure/`. No hay evidencia en el repositorio de que
  esto sea una decisión documentada; se registra aquí como hecho verificado
  y como inconsistencia frente al patrón usado por Inventory (y, se asume,
  por el resto de módulos según ADR-0001).
- **Endpoints de lectura incompletos**: `ReceiptsController.Get` e
  `InvoicesController.Get` son stubs que devuelven `{ id }`, no la entidad
  real; el frontend de detalle no podría consumirlos tal cual.
- **Desajuste frontend/backend**: el frontend modela `PurchaseOrder`,
  `SupplierInvoice` y `GoodsReceipt` con campos que no existen en el backend
  (`supplierId`/`supplierName`, `status` con estados como `Open` /
  `PartiallyReceived` / `Completed` / `Cancelled` / `Draft` / `Approved` /
  `Paid`, `subtotal`/`taxAmount`/`total`, `deliveredQuantity`/`billedQuantity`
  por línea, endpoint `PATCH .../cancel`). Ninguno de esos campos ni la
  acción `cancel` existen en las entidades, DTOs, comandos ni controllers
  reales. Además, las páginas de listado llaman a
  `/api/proxy/purchasing/...` (sin versión) mientras que las de alta/detalle
  llaman a `/api/proxy/v1/purchasing/...`; solo la segunda ruta coincide con
  el prefijo `api/v{version}/...` que exponen los controllers.
- **Proveedor no vinculado**: al no persistirse `SupplierId` en Purchasing,
  no hay forma de saber en base de datos a qué proveedor de CRM pertenece
  un pedido, una recepción o una factura; cualquier reporting o
  contabilización futura que necesite esa relación requiere añadir la
  columna y el flujo correspondiente.
- **Sin integración real con Inventory ni Accounting**: registrar una
  recepción no mueve stock, y crear una factura de proveedor no genera
  asiento contable ni obligación de pago en Treasury; el módulo cubre solo
  el registro documental del pedido, la recepción y la factura, más el
  control de three-way match en el momento de crear la factura.
- El validador de three-way match no persiste su resultado (no hay estado
  de "conciliado"/"con discrepancias" en `SupplierInvoice`): un fallo
  bloquea la creación por completo, sin posibilidad de guardar la factura
  como pendiente de revisión.

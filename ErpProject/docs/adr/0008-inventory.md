# ADR-0008: Inventory

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Inventory resuelve el control de existencias del ERP: catálogo de
productos, stock por almacén, movimientos de entrada/salida, trazabilidad
por lote/número de serie y valoración del inventario (necesaria para el
coste de ventas y, en última instancia, para la contabilidad). Al tratarse
de un ERP español, la valoración soporta explícitamente el método de Coste
Medio Ponderado (PMP/CMP), habitual en el PGC, además de FIFO.

El código relevante vive en `backend/Modules/Inventory/{API,Application,
Domain,Infrastructure}` (carpeta `API/` en mayúsculas; namespace
`Erp.Modules.Inventory.Api`, ADR-0018 #19f). Todas las entidades
(`Product`, `Warehouse`, `Stock`, `StockMovement`, `Lot`, `SerialNumber`)
están en `Modules/Inventory/Domain/Entities/` (ADR-0018 #14 ✅).

`Program.cs` registra el módulo vía `InventoryErpModule` y namespace
`Erp.Modules.Inventory.Api.Controllers` (ADR-0018 #19f — unificado con el
resto de módulos; la carpeta física sigue siendo `API/` en mayúsculas).
vive en `frontend/src/app/inventory/` (página principal más subpáginas
`lots/`, `serials/`, `valuation/`, `warehouses/`).

## Decisión
### Backend
Seis controllers en `backend/Modules/Inventory/API/Controllers/`:

- **`ProductsController`** (`api/inventory/products`, `[Authorize,
  RequiredModule("Inventory")]`) — CRUD de productos vía MediatR:
  `GET /` (búsqueda/filtrado por `search`, `active`, `type`; **paginado
  backlog #8:** `page`/`pageSize`, header `X-Total-Count`, respuesta
  `{ items, totalCount, page, pageSize }`),
  `GET /{id}`, `POST /` (`CreateProductCommand`),
  `PUT /{id}` (`UpdateProductCommand`),
  `PATCH /{id}/activate` (`SetProductActiveCommand`),
  `GET /{id}/movements` (`GetProductMovementsQuery`, paginado).
- **`WarehousesController`** (`api/inventory/warehouses`) — CRUD de
  almacenes: `GET /`, `GET /{id}`, `POST /` (`CreateWarehouseCommand`),
  `PUT /{id}` (`UpdateWarehouseCommand`),
  `PATCH /{id}/activate` (`SetWarehouseActiveCommand`).
- **`StockController`** (`api/inventory/stock`) —
  `GET /` (stock por almacén/producto, filtro `belowReorderPoint`),
  `GET /{productId}/warehouses` (stock de un producto por almacén),
  `GET /valuation` (valoración agregada, `GetStockValuationQuery`),
  `GET /api/inventory/movements` (histórico paginado de movimientos, con
  `X-Total-Count`), y `POST /adjustment` (`AdjustStockCommand`, ajuste
  manual de stock).
- **`LotsController`** (ruta versionada `api/v{version:apiVersion}/inventory/lots`,
  `1.0`) — CRUD de `Lot` vía MediatR (`LotCommands`/`LotQueries`/`LotHandlers`).
- **`SerialsController`** (ruta versionada `api/v{version:apiVersion}/inventory/serials`,
  `1.0`) — CRUD de `SerialNumber` + `PUT .../status` vía MediatR
  (`SerialCommands`/`SerialQueries`/`SerialHandlers`).
- **`ValuationController`** (ruta versionada `api/v{version:apiVersion}/inventory/valuation`,
  `1.0`) — valoración por producto vía `GetInventoryValuationQuery`/`ValuationHandlers`
  (estrategia PMP/FIFO; método por query o `Inventory:ValuationMethod` en config).
  Los tres controllers versionados no llevan `[Authorize]` ni `[RequiredModule]`
  explícitos, a diferencia de Products/Warehouses/Stock.

CQRS (`backend/Modules/Inventory/Application/Features/Inventory/`):
`Commands/`, `Queries/` y `Handlers/` para `Product*`, `Warehouse*`,
`Stock*`, `Lot*` y `Serial*`/`Valuation*` (`ProductCommands.cs`/`ProductHandlers.cs`/`ProductQueries.cs`,
`LotCommands.cs`/`LotHandlers.cs`/`LotQueries.cs`,
`SerialCommands.cs`/`SerialHandlers.cs`/`SerialQueries.cs`,
`ValuationQueries.cs`/`ValuationHandlers.cs`,
etc.), todos implementados como `IRequest`/`IRequestHandler` de MediatR
sobre `IInventoryDbContext`. No se han encontrado `FluentValidation`
validators específicos del módulo (`Application/Validators/` no existe);
la validación de negocio (stock insuficiente, cantidad cero, producto o
almacén inexistente) se hace a mano en los handlers lanzando
`KeyNotFoundException`/`ArgumentException`/`InvalidOperationException`
(ver `AdjustStockHandler`).

Valoración de inventario
(`backend/Modules/Inventory/Application/Services/InventoryValuationService.cs`):
estrategia intercambiable (`IValuationStrategy`) con dos implementaciones,
`PMPValuation` (coste medio ponderado sobre todos los movimientos de
entrada) y `FIFOValuation` (coste de la primera entrada), seleccionada por
`InventoryValuationService.CreateStrategy(method)` (por defecto `"PMP"`,
configurable vía `Inventory:ValuationMethod`).

Eventos de dominio (`backend/Modules/Inventory/Application/Events/` y
`EventHandlers/`): el módulo **consume** `InvoiceApprovedEvent` (Billing) y
`ExpenseApprovedEvent` (Expenses) — ambos definidos en
`backend/Erp.Application/Common/Events/DomainEvents.cs` — y **publica**
`StockMovementCreatedEvent` hacia el patrón Outbox descrito en ADR-0001.

### Frontend
`frontend/src/app/inventory/page.tsx` — página principal con pestañas
"Productos" (alta/edición de productos, activar/desactivar, botón "Ajustar"
que abre un modal de ajuste manual de stock contra
`POST /api/proxy/inventory/stock/adjustment`) y "Movimientos" (histórico
paginado). Usa el proxy interno `/api/proxy/inventory/...`.

Subpáginas: `warehouses/page.tsx` (CRUD de almacenes y, al seleccionar uno,
ver su stock vía `GET /api/proxy/inventory/stock?warehouseId=...`),
`lots/page.tsx` (listado de lotes), `serials/page.tsx` (listado de números
de serie) y `valuation/page.tsx` (selector de método PMP/FIFO y tabla de
valoración). Estas tres últimas están notablemente menos pulidas que la
página principal: llaman directamente a `/api/v1/inventory/lots`,
`/api/v1/inventory/serials` y `/api/v1/inventory/valuation` (sin pasar por
`/api/proxy/...`), la UI está en inglés ("Product Lots", "Loading...") en
vez de en español como el resto del módulo, y enlazan a rutas de alta
(`/inventory/lots/new`, `/inventory/serials/new`) cuya existencia no se ha
verificado como parte de esta ADR.

### Modelo de datos
Esquema PostgreSQL `inventory` (`InventoryDbContext`, migraciones
`InitialCreate` y `AddLotsAndSerials`). Entidades en
`Modules/Inventory/Domain/Entities/`:

- **`InventoryProducts`** (`Product`) — `SKU`, `Name`, `Type`
  (`Product`/`Service`), `CostPrice` (coste medio calculado), `SalePrice`,
  `VatPercent`, `TrackStock`, `IsActive`, `ReorderPoint`/`ReorderQty`.
- **`Warehouses`** (`Warehouse`) — `Name`, `Location`, `IsActive`.
- **`Stocks`** (`Stock`) — posición de stock por par (`ProductId`,
  `WarehouseId`) con `Quantity`; FK a `Product` y `Warehouse` con
  `OnDelete(Cascade)`; check constraint `CK_Stock_Quantity_NonNegative`
  (`"Quantity" >= 0`) a nivel de base de datos.
- **`StockMovements`** (`StockMovement`) — histórico **inmutable**
  (comentario explícito en el código: "Stock Movements are STRICTLY
  IMMUTABLE") de cada entrada/salida: `MovementType` (`Purchase`, `Sale`,
  `Entry`, `Adjustment`), `Quantity` (con signo), `UnitCost` (coste
  evaluado en el momento del movimiento), `ReferenceType`/`ReferenceId`
  (FK "blanda", sin restricción física, hacia `Invoice`, `Expense` o
  `Manual`).
- **`Lots`** (`Lot`) — `ProductId`, `LotNumber`, `ExpirationDate`,
  `Quantity`, `UnitCost`, `IsActive`.
- **`SerialNumbers`** (`SerialNumber`) — `ProductId`, `LotId` opcional,
  `Serial`, `Status` (`Available`/`Sold`/`Returned`), `SoldDate`.

Todas las entidades llevan `CompanyId` y filtro multi-tenant
(`HasQueryFilter(e => e.CompanyId == TenantContext.TenantId)`) aplicado en
`InventoryDbContext.OnModelCreating`. No hay FKs físicas entre `inventory`
y otros esquemas (coherente con el aislamiento por esquema de ADR-0001):
`StockMovement.ReferenceId` referencia facturas o gastos por `Guid` sin
constraint de base de datos.

### Flujo end-to-end representativo
Entrada de stock al aprobar un gasto de compra (Expenses → Inventory):

1. Un usuario aprueba un documento de gasto en el módulo Expenses; el
   handler correspondiente
   (`backend/Modules/Expenses/Application/Features/Expenses/Handlers/ExpenseCommandHandlers.cs`)
   publica `ExpenseApprovedEvent` (con `Lines`, cada una con `ProductId`,
   `Quantity`, `UnitCost`) vía `IPublisher.Publish`.
2. `ExpenseApprovedInventoryHandler`
   (`backend/Modules/Inventory/Application/EventHandlers/ExpenseApprovedInventoryHandler.cs`),
   suscrito como `INotificationHandler<ExpenseApprovedEvent>`, comprueba
   idempotencia (si ya existen `StockMovement` con
   `ReferenceType == "Expense"` y ese `ReferenceId`, no hace nada), busca
   un almacén activo de la compañía, y por cada línea con producto y
   `TrackStock == true`: recalcula el Coste Medio Ponderado del producto
   (`((currentQty * CostPrice) + (Quantity * UnitCost)) / newQty`),
   inserta un `StockMovement` de tipo `"Purchase"`, e incrementa (o crea)
   el `Stock` del par producto/almacén.
3. Tras `SaveChangesAsync`, publica un `StockMovementCreatedEvent` por
   cada línea procesada.
4. `StockMovementCreatedEventHandler`
   (`backend/Modules/Inventory/Application/EventHandlers/StockMovementCreatedEventHandler.cs`)
   serializa el evento y lo inserta como `OutboxMessage`
   (`EventType = "StockMovementCreatedEvent"`) en `IApplicationDbContext`,
   siguiendo el patrón Outbox de ADR-0001, para que el job Hangfire
   `outbox-processor` lo entregue a los consumidores externos registrados.
5. El flujo simétrico (venta) existe vía `InvoiceApprovedInventoryHandler`,
   que reacciona a `InvoiceApprovedEvent` (Billing) decrementando `Stock` y
   registrando un `StockMovement` de tipo `"Sale"` — con la misma
   comprobación de idempotencia y de stock suficiente (si no hay stock
   suficiente, la línea se omite con un `LogWarning`, no lanza excepción).

## Relación con otros módulos
- **Billing → Inventory**: `InvoiceApprovedInventoryHandler` consume
  `InvoiceApprovedEvent`, publicado desde
  `backend/Modules/Billing/Application/Features/Billing/Handlers/BillingHandlers.cs`,
  para decrementar stock al aprobar una factura de venta.
- **Expenses → Inventory**: `ExpenseApprovedInventoryHandler` consume
  `ExpenseApprovedEvent`, publicado desde
  `backend/Modules/Expenses/Application/Features/Expenses/Handlers/ExpenseCommandHandlers.cs`,
  para incrementar stock (y recalcular el coste medio) al aprobar un gasto.
- **Inventory → Outbox**: `StockMovementCreatedEvent` se escribe siempre en
  `OutboxMessage` para consumo externo; no se ha encontrado ningún handler
  interno de otro módulo que lo consuma directamente en el código actual
  (el comentario del propio handler menciona Accounting/Notifications como
  destinatarios previstos, pero no hay evidencia de esa integración
  implementada).
- **Purchasing:** ✅ Corregido (ADR-0018 #20) — `CreateGoodsReceiptHandler`
  publica `GoodsReceiptCreatedEvent`; `GoodsReceiptInventoryHandler` incrementa
  stock con idempotencia por `ReferenceType=GoodsReceipt`.
- **Sales:** ✅ Corregido (ADR-0018 #21) — `CreateDeliveryNoteHandler`
  publica `DeliveryNoteCreatedEvent`; `DeliveryNoteInventoryHandler`
  decrementa stock. Además persiste el camino vía `InvoiceApprovedEvent`
  (Billing) y `ExpenseApprovedEvent` (Expenses).
- **Multi-tenancy y arquitectura de módulos**: Inventory sigue el patrón
  común de ADR-0001 (`ModuleDbContextBase`, esquema PostgreSQL propio
  `inventory`, filtros `HasQueryFilter` por `CompanyId`, migraciones
  independientes aplicadas en el arranque).

## Buenas prácticas aplicables
- Cualquier movimiento de stock debe pasar por `StockMovement` (registro
  inmutable) y actualizar `Stock` en la misma unidad de trabajo — es el
  patrón seguido tanto por `AdjustStockHandler` como por los `EventHandlers`
  de Billing/Expenses; no modificar `Stock.Quantity` sin dejar rastro en
  `StockMovements`.
- Respetar la idempotencia por `ReferenceType`/`ReferenceId` al añadir
  nuevas integraciones entrantes (comprobar con `AnyAsync` antes de crear
  movimientos), tal y como hacen `InvoiceApprovedInventoryHandler` y
  `ExpenseApprovedInventoryHandler`, para evitar duplicar stock si un
  evento se reprocesa.
- El check constraint `CK_Stock_Quantity_NonNegative` es la única barrera
  real contra stock negativo a nivel de base de datos; los handlers deben
  seguir validando `currentQty + delta >= 0` en aplicación (como hace
  `AdjustStockHandler`) para devolver un error de negocio legible en vez de
  una excepción de base de datos.
- No se ha encontrado control de concurrencia optimista (sin
  `RowVersion`/token de concurrencia en `Stock` ni `Product`); dos ajustes
  simultáneos sobre el mismo par producto/almacén pueden pisarse. Si se
  añade un flujo de alto volumen concurrente, considerar añadir un token de
  concurrencia o mover el update a SQL atómico.
- Si se añaden nuevos endpoints de Lots/Serials/Valuation, alinear su
  estilo con el resto del módulo: usar CQRS vía MediatR (como
  Products/Warehouses/Stock) en vez de acceso directo a
  `IInventoryDbContext` desde el controller, y aplicar `[Authorize,
  RequiredModule("Inventory")]` de forma consistente.

## Consecuencias
- **Namespace `Api`:** ✅ corregido (#19f) — `Erp.Modules.Inventory.Api.Controllers`.
  La carpeta física `API/` en mayúsculas es cosmética (ADR-0018 §3).
- **Entidades:** ✅ todas en `Modules/Inventory/Domain/Entities/` (#14).
- **Inconsistencia interna de estilo entre controllers:** `Products`,
  `Warehouses`, `Stock`, `Lots`, `Serials` y `Valuation` usan CQRS/MediatR;
  `Products`/`Warehouses`/`Stock` usan rutas sin versionar
  (`api/inventory/...`) y `[Authorize, RequiredModule("Inventory")]`; los
  tres controllers versionados (`Lots`/`Serials`/`Valuation`) usan
  `api/v{version}/inventory/...` y no llevan esos atributos de autorización
  explícitos — a confirmar si hay autorización global a nivel de pipeline.
- **Integraciones cruzadas:** recepción de compra y albarán de venta ya mueven
  stock (#20/#21). Permanece el hueco de entrega/facturación solo vía Billing
  si no se usa el flujo Purchasing/Sales.
- **Sin validadores FluentValidation dedicados**: a diferencia de otros
  módulos que registran `IValidator<TRequest>` para sus comandos, Inventory
  valida a mano en los handlers; el pipeline `ValidationBehavior` de
  ADR-0001 no aporta nada aquí porque no hay validators que ejecutar.
- **Frontend desigual**: `lots/`, `serials/` y `valuation/` están en un
  estado visiblemente menos maduro que `page.tsx` y `warehouses/page.tsx`
  (texto en inglés, llamadas directas a `/api/v1/...` en vez de al proxy
  `/api/proxy/...`, enlaces a páginas de alta no verificadas). Antes de dar
  por completa la trazabilidad de lotes/series de cara al usuario, conviene
  homogeneizar estas páginas con el resto del módulo.

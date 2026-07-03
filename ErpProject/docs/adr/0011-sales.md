# ADR-0011: Sales (Pedidos, Entregas y Facturación de Cliente)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Sales (`backend/Modules/Sales`) modela el flujo operativo
order-to-cash: pedido de venta (`SalesOrder`) → albarán de entrega
(`DeliveryNote`) → factura de cliente (`CustomerInvoice`). Es un módulo
más pequeño que Billing o CRM, pero con integraciones verificadas hacia
CRM (cliente), Billing (factura fiscal), e Inventory (stock en entrega).

**Distinción con CRM y Billing:**
- **CRM (ADR-0004):** maestro del cliente. **✅ Corregido (#23):** `SalesOrder.ClientId`
  tiene FK a `crm.Clients`; `CreateSalesOrderHandler` valida vía
  `IClientInfoService` y fija `ClientName` desde CRM.
- **Billing (ADR-0005):** documento con validez legal. **✅ Corregido (#22):**
  `CreateCustomerInvoiceHandler` crea factura fiscal en Billing vía
  `CreateInvoiceCommand`; líneas Sales son operativas; totales header como
  cache desnormalizado desde Billing.

En otras palabras: Sales resuelve "¿qué pedimos, qué entregamos y qué hemos
facturado de ese pedido en términos operativos?", mientras que Billing
resuelve "¿qué documento con validez legal ante la AEAT hemos emitido?".
Cualquier trabajo que necesite unificar ambos flujos debe tratarse como una
decisión de diseño nueva, no asumir que ya existe una integración.

### Estructura del módulo
**✅ Corregido (ADR-0018 #12):** cuatro proyectos separados
(`Api`, `Application`, `Domain`, `Infrastructure`), cada uno con su `.csproj`,
igual que el resto de módulos del monolito.

## Decisión

### Backend
Entidades (`Domain/Entities`, todas `AuditableEntity` con `CompanyId`):
- `SalesOrder` + `SalesOrderLine` — pedido con `Number`, `Status` (Open,
  PartiallyDelivered, Completed), líneas con `Quantity`, `UnitPrice`,
  `TaxRate` (por defecto 21%), y dos contadores de progreso:
  `DeliveredQuantity` y `BilledQuantity`.
- `DeliveryNote` + `DeliveryNoteLine` — albarán ligado a un `SalesOrderId`,
  cada línea referencia una `SalesOrderLineId` y suma `ShippedQuantity`.
- `CustomerInvoice` + `CustomerInvoiceLine` — factura de cliente ligada a un
  `SalesOrderId`; cada línea referencia una `SalesOrderLineId`, con
  `TipoOperacion`, `SurchargeRate/Amount` y `LineTotal` calculado
  (`BilledQuantity * UnitPrice + TaxAmount + SurchargeAmount`).

Controllers versionados (`api/v{version:apiVersion}/sales/...`, a diferencia
del resto de módulos que usan `api/[controller]` sin versión):
- `SalesOrdersController` (`/api/v1/sales/orders`) — `GET`/`GET {id}`/`POST`
  vía `IMediator` (`Application/Features/Orders/SalesOrderHandlers.cs`;
  backlog #11). Filtra por tenant en queries.
- `DeliveriesController` (`/api/v1/sales/deliveries`) — CQRS vía MediatR
  (`CreateDeliveryNoteCommand`/`GetAllDeliveryNotesQuery`).
- `CustomerInvoicesController` (`/api/v1/sales/invoices`) — CQRS vía
  MediatR (`CreateCustomerInvoiceCommand`/`GetAllCustomerInvoicesQuery`).

Lógica de negocio relevante:
- `CreateDeliveryNoteHandler`: por cada línea de albarán, incrementa
  `SalesOrderLine.DeliveredQuantity`, recalcula `SalesOrder.Status`, y
  publica `DeliveryNoteCreatedEvent` (Inventory decrementa stock, #21).
- `CreateCustomerInvoiceHandler`: valida cantidades contra entregas, crea la
  factura fiscal en Billing vía `CreateInvoiceCommand` (#22), guarda
  `BillingInvoiceId`/`BillingInvoiceNumber`, y persiste líneas Sales operativas.

`CreateSalesOrderHandler` valida `ClientId` contra CRM (#23). Sales publica
eventos de dominio hacia Inventory en entregas; la facturación fiscal pasa
por Billing, no es un flujo paralelo desconectado.

### Frontend
`frontend/src/app/sales/`:
- `orders/page.tsx`, `orders/new/page.tsx`, `orders/[id]/page.tsx` — listado,
  alta y detalle de pedidos de venta, con filtro por `Status` (Open,
  Shipped, Delivered, Cancelled — nótese que estos valores de UI no
  coinciden exactamente con los que usa el backend `SalesOrder.Status`
  actualmente, que son Open/PartiallyDelivered/Completed).
- `deliveries/page.tsx`, `deliveries/new/page.tsx` — listado y alta de
  albaranes.
- `invoices/new/page.tsx` — alta de `CustomerInvoice` a partir de un pedido.

Todas llaman a `/api/proxy/v1/sales/...` (nótese el segmento `v1` explícito
en las llamadas del frontend, reflejando el versionado de la API de Sales).

### Modelo de datos
Esquema `sales`. Migraciones:
1. `InitialCreateSales`
2. `AddCustomerInvoiceLineTaxFields` — añade campos de IVA/recargo/tipo de
   operación a `CustomerInvoiceLine` (posteriormente revertido por
   `RemoveFiscalFieldsFromCustomerInvoiceLine` — ADR-0018 #22).

Filtros de tenant (`HasQueryFilter`) sobre `SalesOrder`, `DeliveryNote` y
`CustomerInvoice`. Único índice adicional: `(CompanyId, Number)` único en
`SalesOrder`. No hay FKs declaradas de EF hacia `DeliveryNoteLine`/
`CustomerInvoiceLine` → `SalesOrderLine` (se resuelven por `Guid` suelto en
la capa de aplicación, con `FindAsync` manual en cada handler).

### Flujo end-to-end representativo
Ciclo completo de un pedido:
1. `POST /api/v1/sales/orders` → `CreateSalesOrderCommand` vía `IMediator`
   (`CreateSalesOrderHandler`, backlog #11) — valida cliente en CRM.
2. `POST /api/v1/sales/deliveries` (`CreateDeliveryNoteCommand`) — registra
   envío, actualiza progreso del pedido, publica `DeliveryNoteCreatedEvent`.
3. `POST /api/v1/sales/invoices` (`CreateCustomerInvoiceCommand`) — factura
   en Billing y actualiza `BilledQuantity` en Sales.

El flujo conecta CRM, Inventory (entrega) y Billing (factura fiscal); no es
autocontenido solo en `SalesDbContext`.

## Relación con otros módulos
- **CRM (ADR-0004):** ✅ `ClientId` con FK y validación en alta (#23).
- **Billing (ADR-0005):** ✅ factura fiscal al crear `CustomerInvoice` (#22).
- **Inventory (ADR-0008):** ✅ `DeliveryNoteCreatedEvent` decrementa stock (#21).

## Evaluación de calidad arquitectónica
> Metodología completa en `ADR-0018`.

- **Estructura:** ✅ cuatro `.csproj` (#12).
- **Controllers delgados:** ✅ `SalesOrdersController` vía `IMediator` (#11).
- **N+1:** ✅ corregido en `CreateDeliveryNoteHandler` (#7).
- **Paginación:** ✅ `GetAllCustomerInvoicesQuery`, listados de albaranes —
  patrón a copiar en otros módulos.

## Buenas prácticas aplicables
- No asumir que facturar en Sales (`CustomerInvoice`) genera efectos
  fiscales o contables: no dispara asientos en Accounting ni cumple ninguno
  de los requisitos de Ley Antifraude/Veri*Factu que sí aplica a
  `Billing.Invoice`. Si se necesita ese comportamiento, debe construirse
  explícitamente (p. ej. publicando un evento equivalente a
  `InvoiceApprovedEvent` o llamando a Billing).
- Al añadir dependencias en Sales, respetar las cuatro capas con frontera de
  compilador (#12) — `Api` no debe referenciar `Infrastructure` directamente.
- Verificar `DeliveredQuantity`/`BilledQuantity` antes de tocar la lógica de
  entregas/facturación parcial — son los únicos guardarraíles de negocio
  existentes (evitar entregar o facturar de más).
- Si se decide conectar Sales con Billing o Inventory, seguir el patrón ya
  establecido en el resto del ERP (eventos de dominio vía MediatR +
  Outbox, ver ADR-0001), no llamadas directas entre `DbContext`s.

## Consecuencias
- Las entregas generan movimiento de stock (#21) y la facturación pasa por
  Billing (#22) — el flujo order-to-cash ya no es un silo aislado.
- Sales no escribe en `ActivityLog` de CRM al crear pedidos; timeline CRM
  sigue dependiendo de eventos explícitos si se quiere trazabilidad comercial.
- El versionado de rutas (`api/v1/sales/...`) es consistente con el frontend
  (`/api/proxy/v1/...`), distinto del resto del ERP sin versión.
- Desajuste UI/backend en valores de `SalesOrder.Status` (frontend usa
  Shipped/Delivered/Cancelled; backend Open/PartiallyDelivered/Completed).

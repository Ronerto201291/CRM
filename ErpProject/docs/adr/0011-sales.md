# ADR-0011: Sales (Pedidos, Entregas y Facturación de Cliente)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Sales (`backend/Modules/Sales`) modela el flujo operativo
order-to-cash: pedido de venta (`SalesOrder`) → albarán de entrega
(`DeliveryNote`) → factura de cliente (`CustomerInvoice`). Es un módulo
mucho más pequeño y de menor madurez que Billing o CRM, sin eventos de
dominio ni integración cruzada verificable con otros módulos en el código
actual.

**Distinción con CRM y Billing** (importante para no confundir entidades de
nombre parecido):
- **CRM** (ADR-0004) es el maestro de identidad del tercero: quién es el
  cliente, sus datos de contacto y su NIF. Sales solo guarda un
  `ClientId`/`ClientName` de texto suelto en `SalesOrder`, sin snapshot
  fiscal ni vínculo verificado a `ICrmDbContext`.
- **Billing** (ADR-0005) es el módulo con obligaciones legales/fiscales:
  numeración correlativa por serie/ejercicio, cadena de hashes SHA256,
  bloqueo tras contabilización, Veri*Factu, FacturaE, IRPF, recargo de
  equivalencia, rectificativas. `Sales.CustomerInvoice` **no tiene ninguno**
  de estos mecanismos: es un documento interno del pedido (cuánto de un
  `SalesOrder` se ha facturado), con `TipoOperacion` y `SurchargeRate` como
  únicos campos de similitud superficial con `Billing.InvoiceLine`, pero sin
  `IsLocked`, `Hash`, `Series`/`FiscalYear`/`SequenceNumber`, ni eventos que
  disparen contabilización. En el estado actual del código, Sales y Billing
  son dos flujos de facturación paralelos y **no conectados entre sí**: no
  existe ningún comando que convierta un `SalesOrder`/`CustomerInvoice` en un
  `Invoice` de Billing, ni al revés.

En otras palabras: Sales resuelve "¿qué pedimos, qué entregamos y qué hemos
facturado de ese pedido en términos operativos?", mientras que Billing
resuelve "¿qué documento con validez legal ante la AEAT hemos emitido?".
Cualquier trabajo que necesite unificar ambos flujos debe tratarse como una
decisión de diseño nueva, no asumir que ya existe una integración.

### Inconsistencia estructural del módulo
A diferencia del resto de módulos (Accounting, Billing, Crm, Expenses,
Inventory, Payroll, Treasury), que se compilan como **cuatro** proyectos
separados (`Api`, `Application`, `Domain`, `Infrastructure`, cada uno con su
propio `.csproj` dentro de su subcarpeta), Sales (y también Purchasing) se
compila como un **único** proyecto:
`backend/Modules/Sales/Erp.Modules.Sales.Infrastructure.csproj`, ubicado en
la raíz del módulo (no dentro de `Sales/Infrastructure/`), que referencia
`Erp.Infrastructure` y `Erp.Application` e incluye todo el árbol de carpetas
(`Api/`, `Application/`, `Domain/`, `Infrastructure/`) como parte del mismo
ensamblado. Las carpetas existen con la misma disposición que en los demás
módulos (`Api/Controllers`, `Application/Features/...`,
`Domain/Entities`, `Infrastructure/Data`, `Infrastructure/Migrations`), solo
cambia el empaquetado en proyectos .csproj. Al trabajar en Sales, tener en
cuenta que no hay separación de ensamblados entre capas — cualquier
referencia circular entre `Api` y `Domain`, por ejemplo, compilará sin
avisar, mientras que en otros módulos el propio grafo de `ProjectReference`
lo impediría.

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
  con acceso directo a `ISalesDbContext` (sin MediatR).
- `DeliveriesController` (`/api/v1/sales/deliveries`) — CQRS vía MediatR
  (`CreateDeliveryNoteCommand`/`GetAllDeliveryNotesQuery`).
- `CustomerInvoicesController` (`/api/v1/sales/invoices`) — CQRS vía
  MediatR (`CreateCustomerInvoiceCommand`/`GetAllCustomerInvoicesQuery`).

Lógica de negocio relevante:
- `CreateDeliveryNoteHandler`: por cada línea de albarán, incrementa
  `SalesOrderLine.DeliveredQuantity`; recalcula `SalesOrder.Status`
  comparando la suma de `DeliveredQuantity` contra la suma de `Quantity` de
  todas las líneas del pedido (`Completed` si ya se entregó todo,
  `PartiallyDelivered` en otro caso).
- `CreateCustomerInvoiceHandler`: por cada línea, calcula `TaxAmount` y
  `SurchargeAmount` a partir de la línea, lanza
  `InvalidOperationException("Cannot bill more than delivered")` si
  `BilledQuantity` solicitada supera `DeliveredQuantity` ya registrada en la
  línea del pedido, e incrementa `SalesOrderLine.BilledQuantity`.

No se ha encontrado ningún `IPublisher`/evento MediatR publicado desde
Sales, ni referencias a Inventory (`StockMovement` o similar) en el módulo:
la entrega de un pedido **no** genera automáticamente un movimiento de stock
verificable en el código actual, a diferencia de lo que sí ocurre en
Expenses/Billing vía `InvoiceApprovedEvent.Lines`/`ExpenseApprovedEvent.Lines`
(pensados explícitamente para integración de Inventory).

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
   operación a `CustomerInvoiceLine`.

Filtros de tenant (`HasQueryFilter`) sobre `SalesOrder`, `DeliveryNote` y
`CustomerInvoice`. Único índice adicional: `(CompanyId, Number)` único en
`SalesOrder`. No hay FKs declaradas de EF hacia `DeliveryNoteLine`/
`CustomerInvoiceLine` → `SalesOrderLine` (se resuelven por `Guid` suelto en
la capa de aplicación, con `FindAsync` manual en cada handler).

### Flujo end-to-end representativo
Ciclo completo de un pedido:
1. `POST /api/v1/sales/orders` (`SalesOrdersController.Create`) — crea
   `SalesOrder` con líneas, IVA fijo al 21% calculado inline en el propio
   controller (no en un handler CQRS).
2. `POST /api/v1/sales/deliveries` (`CreateDeliveryNoteCommand`) — registra
   qué cantidad de cada línea se ha enviado; actualiza
   `DeliveredQuantity` y recalcula `SalesOrder.Status`.
3. `POST /api/v1/sales/invoices` (`CreateCustomerInvoiceCommand`) — factura
   lo entregado (valida que no se facture más de lo entregado); actualiza
   `BilledQuantity`.

En ningún paso se publica un evento de dominio ni se llama a otro módulo:
el flujo es autocontenido dentro de `SalesDbContext`.

## Relación con otros módulos
- **CRM** (ADR-0004): `SalesOrder.ClientId`/`ClientName` sugiere una
  referencia a `Client`, pero es un campo de texto/GUID suelto sin
  `IClientInfoService` ni FK — no hay integración verificada en el código,
  a diferencia de Billing.
- **Billing** (ADR-0005): no existe conversión de `SalesOrder`/
  `CustomerInvoice` a `Billing.Invoice`; son flujos paralelos e
  independientes en el estado actual del código.
- **Inventory** (ADR-0008): no se ha encontrado ninguna referencia a
  Inventory ni publicación de eventos de movimiento de stock al registrar
  una entrega — a diferencia de Billing/Expenses, que sí incluyen DTOs de
  líneas pensados para esa integración.

## Buenas prácticas aplicables
- No asumir que facturar en Sales (`CustomerInvoice`) genera efectos
  fiscales o contables: no dispara asientos en Accounting ni cumple ninguno
  de los requisitos de Ley Antifraude/Veri*Factu que sí aplica a
  `Billing.Invoice`. Si se necesita ese comportamiento, debe construirse
  explícitamente (p. ej. publicando un evento equivalente a
  `InvoiceApprovedEvent` o llamando a Billing).
- Al añadir dependencias nuevas en Sales, recordar que el módulo compila
  como un único proyecto: no hay barrera de compilación entre capas, así
  que la disciplina de "Api no debe llamar directamente a Infrastructure"
  depende solo de la convención, no del compilador.
- Verificar `DeliveredQuantity`/`BilledQuantity` antes de tocar la lógica de
  entregas/facturación parcial — son los únicos guardarraíles de negocio
  existentes (evitar entregar o facturar de más).
- Si se decide conectar Sales con Billing o Inventory, seguir el patrón ya
  establecido en el resto del ERP (eventos de dominio vía MediatR +
  Outbox, ver ADR-0001), no llamadas directas entre `DbContext`s.

## Consecuencias
- La falta de eventos de dominio significa que Sales no aparece en ningún
  timeline de CRM (`ActivityLog`) ni genera asientos contables — cualquier
  reporte financiero que dependa de "todo lo vendido" debe mirar a Billing,
  no a Sales, mientras no se conecten ambos flujos.
- El versionado de rutas (`api/v1/sales/...`) y el uso de `/api/proxy/v1/...`
  en el frontend son consistentes entre sí, pero distintos del resto del
  ERP (`api/[controller]` sin versión) — tenerlo en cuenta al añadir nuevos
  endpoints en este módulo para mantener el patrón ya iniciado aquí.
- El único proyecto `.csproj` compartido reduce el ceremonial de compilar
  cuatro ensamblados, pero es una excepción a la convención de ADR-0001;
  cualquier tooling que asuma "cuatro csproj por módulo" (scripts de build,
  generadores) debe tener en cuenta a Sales y Purchasing como casos
  especiales.

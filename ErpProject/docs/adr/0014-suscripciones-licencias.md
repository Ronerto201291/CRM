# ADR-0014: Suscripciones y Licencias (SaaS billing)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Este módulo resuelve una pregunta distinta de la de ADR-0005 (Billing):
**cómo cobra DevCorp (el proveedor del ERP) a cada empresa-tenant por
usar el propio ERP**, no cómo un tenant factura a sus clientes. Es fácil
confundir ambos porque comparten vocabulario ("factura", "suscripción",
"Stripe"):

- **ADR-0005 Billing** = facturación española (RD 1619/2012, SII,
  VeriFactu) que una empresa-tenant emite a **sus** clientes finales.
  Vive en `backend/Modules/Billing/`.
- **ADR-0014 (esta)** = suscripción SaaS que **DevCorp** cobra a la
  empresa-tenant por el acceso al ERP (planes, módulos incluidos,
  checkout y facturación de Stripe). Vive en los proyectos "core"
  (`Erp.Api`, `Erp.Application`, `Erp.Domain`, `Erp.Infrastructure`),
  bajo el namespace `Licensing`, **no** bajo `backend/Modules/`.

Este módulo también es el que decide, en tiempo real, qué módulos de
negocio (CRM, Billing, Accounting, Inventory, etc.) puede usar cada
tenant, mediante `TenantModule` — es la puerta de entrada a todo lo
demás.

## Decisión

### Backend
Tres entidades en `backend/Erp.Domain/Entities/Licensing/`:

- `Plan` (`Name`, `Description`, `MonthlyPrice`, `YearlyPrice`,
  `MaxUsers`, `MaxInvoicesPerMonth`, `IsActive`, `SortOrder`) — catálogo
  de planes SaaS (Free/Starter/Professional/Enterprise, según el
  comentario del entity).
- `PlanModule` (`PlanId`, `ModuleName`, `IsIncluded`) — qué módulos de
  negocio incluye cada plan (p. ej. `"Inventory"`, `"OCR"`, `"PublicApi"`).
- `Subscription` (`CompanyId`, `PlanName`, `ExpirationDate`,
  `ActiveModules` JSONB, `IsActive`, `StripeSubscriptionId`,
  `StripeStatus`) — la suscripción activa de un tenant.

Y, en `backend/Erp.Domain/Entities/Core/TenantModule.cs`:
`TenantModule` (`CompanyId`, `ModuleName`, `IsEnabled`) — **override
por-tenant** de si un módulo concreto está habilitado, independiente del
plan. Se gestiona vía `TenantModulesController`
(`GET/PUT api/tenant/modules`, `[Authorize]`) y se sincroniza
automáticamente desde el plan tras cada evento de pago de Stripe (ver
más abajo).

**Controllers** (`backend/Erp.Api/Controllers/Subscriptions/`):

- `SubscriptionController` (`api/subscription`, `[Authorize]`):
  `GET` (plan/estado actual), `GET /plans` (catálogo de planes activos
  con sus módulos), `POST /checkout` (crea sesión de Stripe Checkout),
  `POST /portal` (crea sesión del Customer Portal de Stripe para
  autogestión), `GET /invoices` (últimas 12 facturas de Stripe del
  tenant, leídas directamente de la API de Stripe, no de la base de
  datos local).
- `StripeWebhookController` (`api/stripe/webhook`, `[AllowAnonymous]`,
  excluido de autenticación y de resolución de tenant — ver
  `TenantResolverMiddleware.IsProtectedRoute`): recibe eventos de
  Stripe verificados por firma HMAC-SHA256 (`Stripe-Signature` +
  `StripeOptions.WebhookSecret`).

**`StripeService`** (`backend/Erp.Infrastructure/Services/StripeService.cs`)
centraliza toda la integración con Stripe:

- `CreateCheckoutSessionAsync` — crea/reutiliza el `Customer` de Stripe
  para la `Company`, resuelve el `Price` desde `StripeOptions.PriceIds`
  (configurado por variables de entorno `Stripe__PriceIds__<Plan>`, nunca
  hardcodeado) y crea la sesión de Checkout en modo `subscription`.
- `HandleWebhookAsync` procesa: `checkout.session.completed`,
  `customer.subscription.updated`, `customer.subscription.deleted`,
  `invoice.payment_succeeded`, `invoice.payment_failed`. Cada handler
  actualiza `Subscription` (estado, fecha de expiración, plan) y, en los
  casos de alta/renovación/cambio de plan, llama a
  `SyncTenantModulesAsync`.
- `SyncTenantModulesAsync(companyId, planName)` — recorre
  `Plan.PlanModules` y crea/actualiza las filas `TenantModule`
  correspondientes (`IsEnabled = PlanModule.IsIncluded`). Es el único
  punto del código que escribe `TenantModule` de forma masiva; el otro
  es el `PUT /api/tenant/modules/{id}` manual del propio tenant.

**Gate de acceso a módulos — `ModuleAuthorizationHandler`**
(`backend/Erp.Infrastructure/Security/ModuleAuthorizationHandler.cs`),
usado junto con el atributo `[RequiredModule("...")]` y el filtro MVC
`ModuleAuthorizationFilter` (registrado globalmente en `Program.cs` vía
`options.Filters.AddService<...>`, ejecuta antes que el filtro ABAC de
ADR-0002). Para cada request a un endpoint marcado, comprueba en orden:

1. Hay tenant resuelto (`ITenantContext.TenantId`).
2. Existe una `Subscription` activa y no caducada
   (`IsActive && ExpirationDate > UtcNow`).
3. El `Plan` de esa suscripción incluye el módulo pedido
   (`PlanModule.IsIncluded`).
4. El `TenantModule` de ese tenant y módulo está `IsEnabled` (override
   por-tenant, permite desactivar un módulo incluido en el plan).

Si falla cualquiera de los cuatro pasos, deniega (`context.Fail()` →
403). Este es el mecanismo real de "gating": un plan que no incluye
`"Inventory"` hace que cualquier endpoint de Inventory marcado con
`[RequiredModule("Inventory")]` devuelva 403, aunque el usuario esté
autenticado y tenga permisos ABAC.

### Frontend
`frontend/src/app/settings/subscription/page.tsx`: consume
`GET /api/proxy/subscription/plans`, `GET /api/proxy/subscription`,
`GET /api/proxy/tenant/modules`, `GET /api/proxy/subscription/invoices`.
Muestra el catálogo de planes con precio mensual/anual, la suscripción
activa, un toggle por módulo (`PUT /api/proxy/tenant/modules/{id}`) y el
historial de facturas de Stripe. Los botones "Cambiar plan" y
"Gestionar suscripción" redirigen respectivamente a Stripe Checkout y al
Customer Portal (`window.location.href` a la URL devuelta por el
backend).

### Modelo de datos
Esquema `public` (vía `ErpDbContext`, no un DbContext de módulo propio):
`Plan`, `PlanModule`, `Subscription`, `TenantModule`. `ErpDbContext`
implementa tanto `IApplicationDbContext` como `ILicensingDbContext`; este
segundo interfaz (`backend/Erp.Application/Common/Interfaces/ILicensingDbContext.cs`)
expone únicamente `TenantModules`, `Subscriptions`, `Plans`,
`PlanModules` — pensado para aislar el gate de autorización
(`ModuleAuthorizationHandler`) de las demás tablas "core". `Company`
tiene un campo `StripeCustomerId` que enlaza cada tenant con su cliente
de Stripe.

### Flujo end-to-end representativo
Alta de suscripción y activación de módulos:

1. El tenant elige un plan en `/settings/subscription` →
   `POST /api/subscription/checkout` → `StripeService.CreateCheckoutSessionAsync`
   crea/reutiliza `Customer` en Stripe y devuelve la URL de Checkout.
2. El usuario paga en Stripe; Stripe llama a
   `POST /api/stripe/webhook` con `checkout.session.completed`.
3. `StripeWebhookController` verifica la firma y delega en
   `StripeService.HandleWebhookAsync` → `HandleCheckoutCompleted`: crea o
   actualiza la fila `Subscription` (`PlanName`, `StripeStatus = "active"`,
   `IsActive = true`, `ExpirationDate = UtcNow.AddYears(1)`).
4. Se llama a `SyncTenantModulesAsync`, que crea/actualiza una fila
   `TenantModule` por cada `PlanModule` del plan contratado.
5. En la siguiente request a cualquier endpoint `[RequiredModule("X")]`,
   `ModuleAuthorizationHandler` ya ve la suscripción activa y el
   `TenantModule` habilitado, y deja pasar la petición.

## Relación con otros módulos
- **Todos los módulos de negocio (0004-0013):** dependen de este módulo
  para el gate de acceso vía `[RequiredModule]` +
  `ModuleAuthorizationHandler`. Sin una `Subscription` activa que
  incluya el módulo, y sin el `TenantModule` correspondiente habilitado,
  cualquier endpoint protegido devuelve 403 aunque el JWT sea válido
  (ADR-0002).
- **ADR-0002 (multi-tenancy y autenticación):** el gate de módulo se
  ejecuta como filtro adicional sobre el mismo `ITenantContext`
  resuelto por `TenantResolverMiddleware`; es un nivel de autorización
  distinto y posterior al JWT.
- **Stripe (externo):** única integración de pago del sistema. El
  webhook es la única ruta explícitamente excluida tanto de
  autenticación (`[AllowAnonymous]`) como de resolución de tenant
  (listada en `TenantResolverMiddleware.IsProtectedRoute`).
- **ADR-0005 Billing:** módulo independiente y frecuentemente
  confundido por nombre; no comparte entidades ni DbContext con este.

## Buenas prácticas aplicables
- Cualquier módulo de negocio nuevo debe decidir su `ModuleName` (el
  string usado en `PlanModule.ModuleName` / `TenantModule.ModuleName` /
  `[RequiredModule("...")]`) de forma consistente entre las tres capas;
  no hay una enumeración compartida que lo valide en tiempo de
  compilación — es texto libre.
- El `StripeOptions.PriceIds` debe mantenerse en variables de entorno
  (`Stripe__PriceIds__<PlanName>`), nunca hardcodeado; `GetStripePriceId`
  lanza excepción explícita si falta.
- Al añadir un nuevo evento de Stripe a manejar, seguir el patrón de
  `HandleWebhookAsync`: buscar la `Subscription` por
  `StripeSubscriptionId` con `IgnoreQueryFilters()` (el webhook no tiene
  tenant resuelto) y llamar a `SyncTenantModulesAsync` si el cambio
  afecta a qué módulos debe tener activos el tenant.

## Auditoría de corrección frente a especificación externa — Stripe
Verificado con lectura completa de `StripeService.cs`/`StripeWebhookController`.
A diferencia de VeriFactu/SII/FacturaE, **la verificación de firma del
webhook está bien hecha**: usa `EventUtility.ConstructEvent` sobre el body
crudo (no reserializado) antes de confiar en el payload
(`StripeService.cs:141`), y devuelve 400 si falta la firma. Hallazgos reales
más allá de eso:
- **Sin idempotencia por `stripeEvent.Id`** (riesgo alto): ~~no hay tabla de
  eventos procesados~~ **✅ Corregido (ADR-0018 #0e)** — `StripeWebhookEvents` +
  skip si `event.Id` ya procesado. Pendiente: distinción test/live de clave API.
- **Sin distinción test/live de la clave API**: a diferencia del guard
  PRE/PROD de `VerifactuOptions`, no hay ninguna comprobación de que
  `Stripe:SecretKey` sea `sk_live_`/`sk_test_` acorde al entorno.
- **Fechas de expiración inconsistentes**: `HandleCheckoutCompleted` fija
  `AddYears(1)` mientras que las renovaciones vía webhook usan `AddMonths(1)`
  — no reflejan el periodo real devuelto por Stripe.
- **Dependencia de un único evento para activar**: la activación depende
  solo de `checkout.session.completed`; si ese webhook concreto no llega,
  el cliente pagó pero `IsActive` se queda en `false`.
- Riesgo menor a vigilar: el código usa `Invoice.SubscriptionId` y
  `Subscription.CurrentPeriodEnd`, campos que Stripe ha ido moviendo de
  sitio en versiones de API más recientes — verificar que coincide con la
  versión de API configurada en la cuenta real antes de ir a producción.

## Consecuencias
- El campo `Subscription.ActiveModules` (JSONB) se escribe con una lista
  fija de módulos al dar de alta una `Company` (`RegisterCompanyCommand`,
  `InviteCompanyCommand`), pero no se ha encontrado código que lo lea
  después — la fuente de verdad real de "qué módulos tiene un tenant" en
  tiempo de ejecución es la tabla `TenantModule`, consultada por
  `ModuleAuthorizationHandler`. `ActiveModules` queda como dato inerte
  tras el alta; es candidato a limpieza o a documentar como obsoleto.
- `HandleCheckoutCompleted` fija `ExpirationDate = UtcNow.AddYears(1)`
  de forma fija en el alta, independientemente de si el plan elegido es
  mensual o anual (`CheckoutRequest` solo lleva `PlanName`, no el ciclo
  de facturación); la renovación real llega después vía
  `invoice.payment_succeeded`, que sí ajusta a un mes. Esto puede dar una
  fecha de expiración inicial optimista si el primer pago falla antes de
  la primera renovación.
- El gate de `ModuleAuthorizationHandler` depende de que cada endpoint
  de negocio esté efectivamente decorado con `[RequiredModule]`; si un
  controller nuevo lo olvida, queda accesible para cualquier tenant
  autenticado independientemente de su plan — es una responsabilidad
  manual, no forzada por el compilador.

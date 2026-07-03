# ADR-0001: Arquitectura general

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El ERP es un SaaS multi-tenant para PYMES españolas construido como un
**monolito modular**: un único proceso backend (`Erp.Api`) que aloja varios
módulos de negocio (CRM, Facturación, Contabilidad, Gastos, Inventario,
Nóminas, Compras, Ventas, Tesorería) implementados como bibliotecas
independientes con arquitectura en capas propia (Clean Architecture). El
stack, según `/home/user/CRM/ErpProject/README.md`, es:

| Capa | Tecnología |
|------|-----------|
| Backend | .NET 10 · ASP.NET Core · Clean Architecture · CQRS (MediatR) · EF Core |
| Frontend | Next.js 15 · TypeScript · Tailwind CSS |
| Base de datos | PostgreSQL 16 |
| Cache | Redis 7 |
| Jobs | Hangfire |
| OCR | Tesseract (local) |
| Deploy | Docker Compose · Nginx · Let's Encrypt |

Esta ADR describe el patrón arquitectónico **compartido por todos los
módulos**: cómo se organiza el código, cómo se registran los módulos en el
host único, cómo se comunican entre sí sin acoplarse directamente, y qué
plumbing común (CQRS, validación, auditoría) usan todos ellos. Los detalles
específicos de cada módulo de negocio se documentan en sus propias ADR
(0004-CRM, 0005-Billing, 0006-Accounting, etc.).

## Decisión

### Backend
El backend se organiza en cuatro proyectos "núcleo" en `backend/`:

- `Erp.Api` — host único de ASP.NET Core, `Program.cs`, controllers
  transversales (Auth, Users, Company, Permissions, ApiKeys, AuditLogs,
  Fiscal, Sii, Tax, Reports, TenantModules, Subscriptions).
- `Erp.Application` — plumbing CQRS común (`Erp.Application.Common`),
  interfaces compartidas (`IApplicationDbContext`, `ITenantContext`,
  `IJwtProvider`, etc.), y features transversales (Auth, Users).
- `Erp.Domain` — entidades base (`AuditableEntity`, `BaseEntity`),
  entidades "core" (`User`, `Role`, `Company`, `Permission`...) y la entidad
  `OutboxMessage`.
- `Erp.Infrastructure` — `ErpDbContext` (el DbContext "core"), seguridad,
  tenancy, interceptores, jobs en background.

Cada módulo de negocio vive bajo `backend/Modules/<Nombre>/` y repite
siempre la misma subestructura de cuatro proyectos, con su propio
`.csproj` cada uno:

```
backend/Modules/<Nombre>/
├── Api/            Erp.Modules.<Nombre>.Api.csproj      (Controllers)
├── Application/    Erp.Modules.<Nombre>.Application.csproj (Commands/Queries/Handlers, MediatR)
├── Domain/         Erp.Modules.<Nombre>.Domain.csproj   (Entidades del módulo)
└── Infrastructure/ Erp.Modules.<Nombre>.Infrastructure.csproj (DbContext propio + Migrations/)
```

Verificado en `backend/Modules/Crm/` y `backend/Modules/Billing/` (y se
repite en Inventory, Accounting, Expenses, Treasury, Payroll, Purchasing,
Sales). Cada módulo tiene:

- Su **propio DbContext** (`CrmDbContext`, `BillingDbContext`, etc.) que
  hereda de `ModuleDbContextBase` (`backend/Erp.Infrastructure/Data/ModuleDbContextBase.cs`).
  Esta clase base centraliza dos comportamientos comunes a todos los
  módulos en `SaveChangesAsync`: (1) timestamps automáticos de
  `AuditableEntity` (`CreatedAt`/`UpdatedAt`), y (2) asignación automática
  de `CompanyId` desde `ITenantContext` en entidades nuevas cuando el
  campo llega vacío.
- Su **propio historial de migraciones EF Core** bajo
  `Infrastructure/Migrations/`, aplicado de forma independiente en el
  arranque (ver más abajo).
- Un **esquema PostgreSQL propio** (`crm`, `billing`, `inventory`,
  `accounting`, `expenses`, `payroll`, `purchasing`, `sales`, `treasury`),
  fijado vía `modelBuilder.HasDefaultSchema("...")` en el `OnModelCreating`
  de cada DbContext — confirmado en los nueve módulos
  (`grep HasDefaultSchema backend/Modules/*/Infrastructure/Data/*.cs`).

Todos los módulos se registran en un único host ASP.NET Core desde
`backend/Erp.Api/Program.cs` mediante **autoregistro modular** (ADR-0018 #19e):

- Cada módulo expone `*ErpModule` (`IErpModule`) con
  `AddErpModule(instance, configuration)` y
  `AddErpModuleControllers(instance)` — p. ej.
  `Erp.Modules.Inventory.Api.InventoryErpModule.Instance`. `Program.cs` no
  lista tipos de controller ni bloques `AddMediatR` por módulo manualmente.
- Cada módulo registra su DbContext, servicios, handlers MediatR y validators
  en su propio `DependencyInjection.cs` / `*ErpModule`.
- En el arranque, cada `DbContext` (core + los nueve módulos) ejecuta
  `Database.MigrateAsync()` de forma independiente.

**CQRS con MediatR.** La aplicación sigue CQRS: comandos y queries son
clases `IRequest<T>` gestionadas por handlers `IRequestHandler<T,R>`,
tanto en `Erp.Application.Features.*` (transversal) como en
`Erp.Modules.<Nombre>.Application.Features.*` (por módulo). El único
pipeline behavior de MediatR registrado es
`ValidationBehavior<TRequest,TResponse>`
(`backend/Erp.Application/Common/Behaviors/ValidationBehavior.cs`),
enlazado en `backend/Erp.Application/DependencyInjection.cs` vía
`cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>))`.
Ejecuta todos los `IValidator<TRequest>` de FluentValidation registrados
para ese comando/query antes de invocar el handler, lanzando
`ValidationException` si falla alguno.

**Eventos de dominio y outbox transaccional.** La comunicación entre
módulos no usa llamadas directas a otros módulos, sino el **patrón
Outbox**: `OutboxMessage` (`backend/Erp.Domain/Entities/Outbox/OutboxMessage.cs`)
es una entidad con `CompanyId`, `EventType`, `Payload` (JSON), `Status`
(`Pending`/`Processed`/`Failed`) y contador de reintentos. Cuando ocurre un
evento de dominio (p. ej. `LeadCreatedEvent`, `ClientCreatedEvent`), un
`INotificationHandler<TEvent>` específico del módulo (p. ej. los handlers
en `backend/Modules/Crm/Application/EventHandlers/CrmEventsOutboxHandler.cs`,
o `backend/Modules/Billing/Application/EventHandlers/PaymentReceivedOutboxHandler.cs`)
serializa el evento y lo inserta como fila `OutboxMessage` **en la misma
transacción** que la operación de negocio, garantizando entrega
"at-least-once" sin pérdida silenciosa si el proceso cae entre el commit y
el procesamiento. Un job recurrente de Hangfire, `outbox-processor`
(`Erp.Infrastructure.Services.OutboxProcessorJob`, registrado en
`Program.cs` con `Cron.Minutely()`), procesa los mensajes pendientes.
El duplicado huérfano `OutboxMessageProcessorJob` fue eliminado (ADR-0018 #19).
RabbitMQ es opcional (`RabbitMQ:Enabled`, por defecto `false`); si está
desactivado, el propio job de Hangfire actúa como transporte de fallback.

**Interceptores de EF Core / auditoría.** `AuditSaveChangesInterceptor`
(`backend/Erp.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`)
está registrado en `ErpDbContext` y en los nueve DbContext de módulo (vía
cada `DependencyInjection` del módulo). Genera filas `AuditLog` con hash
SHA-256 a partir del `ChangeTracker` en cada `SaveChangesAsync` (ADR-0018 #31).
La clase estática `AuditInterceptor.ProcessAuditEntries` sigue en el repo como
referencia histórica pero **no** es la implementación activa. Algunos handlers
(Billing, Expenses) además escriben `AuditLog` manualmente en puntos críticos
fiscales — ver ADR-0017.

### Frontend
El frontend es una única aplicación Next.js 15 (App Router) en
`frontend/src/app/`, con una carpeta por módulo de negocio
(`crm/`, `billing/`, `accounting/`, `expenses/`, `inventory/`, `payroll/`,
`purchasing/`, `sales/`, `treasury/`, `fiscal/`, `sii/`, `verifactu/`) más
carpetas transversales (`dashboard/`, `settings/`, `admin/`, `register/`,
`signup/`, `api/`). No hay separación en aplicaciones o paquetes distintos
por módulo: todas las rutas comparten el mismo proceso Next.js, el mismo
middleware (`frontend/src/middleware.ts`) y el mismo contexto de tenant
(`frontend/src/context/TenantContext.tsx`), que se documentan en detalle en
ADR-0002.

### Modelo de datos
Una única instancia de PostgreSQL 16 aloja todos los datos, particionados
por **esquema** (no por base de datos): `public` (entidades core: `User`,
`Role`, `Company`, `Permission`, `AuditLog`, `Subscription`, `ApiKey`,
`OutboxMessage`, etc., vía `ErpDbContext`) y un esquema dedicado por módulo
de negocio (`crm`, `billing`, `inventory`, `accounting`, `expenses`,
`payroll`, `purchasing`, `sales`, `treasury`). Cada módulo es dueño
exclusivo de las tablas de su esquema y de su propio historial de
migraciones EF Core (`__EFMigrationsHistory` por esquema). No hay FKs de
base de datos entre esquemas de módulos distintos; la relación entre
entidades de módulos distintos (p. ej. una factura de Billing que referencia
un cliente de CRM) se resuelve por `Guid` sin FK física, coherente con el
aislamiento físico buscado.

### Flujo end-to-end representativo
Ejemplo: alta de un lead en CRM que termina notificando a otros posibles
consumidores vía outbox.

1. El frontend en `frontend/src/app/crm/...` envía un `POST` al backend.
2. El controller correspondiente en
   `backend/Modules/Crm/Api/Controllers/` despacha un comando MediatR
   (p. ej. `CreateLeadCommand`) hacia `Erp.Modules.Crm.Application`.
3. El handler valida (vía `ValidationBehavior` + FluentValidation), persiste
   la entidad `Lead` en `CrmDbContext` (esquema `crm`), y publica
   `LeadCreatedEvent` a través de MediatR (`INotification`).
4. `LeadCreatedOutboxHandler`
   (`backend/Modules/Crm/Application/EventHandlers/CrmEventsOutboxHandler.cs`)
   recibe la notificación, serializa el evento a JSON y lo inserta como fila
   `OutboxMessage` (`CompanyId`, `EventType = "LeadCreatedEvent"`) en la
   misma unidad de trabajo.
5. El job recurrente `outbox-processor` (Hangfire, cada minuto) recoge los
   mensajes `Pending` y los entrega a los consumidores registrados
   (RabbitMQ si está activo, o el propio pipeline de Hangfire si no).

### Otros elementos compartidos
- **API versionada por ruta** vía `Asp.Versioning` (`AddApiVersioning`,
  versión por defecto `v1.0`).
- **Health checks**: `/health` (completo), `/health/live` (liveness, sin
  dependencias), `/health/ready` (solo checks etiquetados `ready`: Postgres
  y Redis).
- **Hangfire Dashboard** en `/hangfire`, restringido a peticiones locales
  (`LocalRequestsOnlyAuthorizationFilter`).
- **Redis** como caché distribuida (usado, entre otros, por
  `PermissionService` para cachear permisos resueltos — ver ADR-0002).

## Relación con otros módulos
Esta ADR describe la infraestructura que **todos** los módulos comparten:
el host único, el patrón CQRS/MediatR, el outbox transaccional para
comunicación desacoplada entre módulos, y la base común de DbContext por
módulo (`ModuleDbContextBase`). Cualquier módulo de negocio (0004-0013)
depende de esta arquitectura para: registrar sus controllers en el host,
gestionar su propio esquema/migraciones, y publicar/consumir eventos de
dominio sin acoplarse directamente a otros módulos. La resolución de
`CompanyId` (multi-tenancy) y de permisos, aunque se apoya en el mismo
`ModuleDbContextBase`, se documenta en detalle en ADR-0002.

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

- **Dirección de dependencias:** ✅ corregido (#13) — `Erp.Infrastructure` ya no
  referencia `*.Application` de módulos; integración cross-módulo vía puertos
  en `Erp.Application`.
- **Controllers delgados:** ✅ 43/43 controllers con `IMediator`/`ISender` (#3c–#11).
- **Registro de módulos:** ✅ `IErpModule` + `AddErpModule` (#19e); sustituye el
  patrón frágil de anclas `AddMediatR` en `Program.cs`.
- **Tests:** 🟡 ampliado (#32) — `Erp.ArchitectureTests` + tests unitarios/
  integración; sin Testcontainers en CI todavía.
- **Pendiente:** Sales/Purchasing tenían un solo assembly — ✅ corregido (#12):
  cuatro `.csproj` por módulo; todos los módulos en `Erp.slnx`.

## Buenas prácticas aplicables
- Todo módulo nuevo debe seguir la misma subestructura de cuatro proyectos
  (`Api`/`Application`/`Domain`/`Infrastructure`) y su propio DbContext
  heredando de `ModuleDbContextBase`, con `HasDefaultSchema` propio.
- La comunicación entre módulos debe hacerse vía eventos de dominio +
  Outbox, no referenciando directamente el `DbContext` o las entidades de
  otro módulo.
- Cualquier `IRequest`/`IRequestHandler` nuevo debe tener su
  `IValidator<T>` de FluentValidation si requiere validación de entrada,
  ya que es el único pipeline behavior activo.
- **Tests automatizados:** existe `Erp.ArchitectureTests` (controllers sin
  DbContext directo, Domain sin EF) y tests unitarios/integración parciales
  (ADR-0018 #32, #35). No sustituyen smoke test manual en flujos fiscales.
- La auditoría automática está activa vía `AuditSaveChangesInterceptor` en
  todos los DbContext; no añadir `new AuditLog{...}` salvo casos fiscales
  que requieran formato/hash específico además del interceptor (#31).

## Consecuencias
- **`Erp.slnx`:** ✅ actualizado (#12) — registra los cuatro proyectos de
  cada uno de los nueve módulos de negocio más el núcleo.
- El **outbox** garantiza entrega at-least-once pero no order-preserving
  entre distintos tipos de evento; los consumidores deben ser idempotentes.
- **Auditoría transversal:** `AuditSaveChangesInterceptor` registra cambios en
  todos los módulos; `settings/audit-logs` consulta datos reales (#31).
- **Exception handling:** `ExceptionHandlingMiddleware` devuelve
  `application/problem+json` (#33).
- La cobertura de tests sigue siendo limitada frente a la complejidad fiscal;
  priorizar integración en flujos críticos antes de refactors amplios (#32).

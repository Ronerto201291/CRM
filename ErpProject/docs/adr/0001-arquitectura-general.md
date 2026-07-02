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
`backend/Erp.Api/Program.cs`:

- Los controllers de cada módulo se añaden como "application parts" del
  host principal (líneas ~53-61):
  ```csharp
  .AddApplicationPart(typeof(Erp.Modules.Billing.Api.Controllers.InvoicesController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Crm.Api.Controllers.ClientsController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Inventory.API.Controllers.ProductsController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Accounting.Api.Controllers.AccountingController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Expenses.Api.Controllers.ExpensesController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Treasury.Api.Controllers.TreasuryController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Payroll.Api.Controllers.PayrollController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Purchasing.Api.Controllers.ReceiptsController).Assembly)
  .AddApplicationPart(typeof(Erp.Modules.Sales.Api.Controllers.SalesOrdersController).Assembly);
  ```
- Cada módulo expone un método `Add<Nombre>Infrastructure(configuration)`
  que registra su DbContext y sus servicios (p. ej.
  `builder.Services.AddCrmInfrastructure(builder.Configuration)`).
- Los handlers de MediatR de cada módulo se registran explícitamente con
  `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(...))`, uno por
  módulo, apuntando a un handler concreto de esa ensambladura como ancla.
- En el arranque, cada `DbContext` (core + los nueve módulos) ejecuta
  `Database.MigrateAsync()` de forma independiente (líneas ~357-369), de
  modo que cada módulo gestiona su propio historial de esquema.

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
`Program.cs` con `Cron.Minutely()`), procesa los mensajes pendientes;
también existe `Erp.Infrastructure.BackgroundJobs.OutboxMessageProcessorJob`.
RabbitMQ es opcional (`RabbitMQ:Enabled`, por defecto `false`); si está
desactivado, el propio job de Hangfire actúa como transporte de fallback.

**Interceptores de EF Core / auditoría.** Existe
`backend/Erp.Infrastructure/Interceptors/AuditInterceptor.cs`, una clase
estática (`AuditInterceptor.ProcessAuditEntries`) pensada para generar
filas `AuditLog` a partir del `ChangeTracker` de un `DbContext`. Sin
embargo, una búsqueda en todo el backend
(`grep -rn "AuditInterceptor" backend --include=*.cs`) muestra que **no
está registrada como `IInterceptor` de EF Core ni invocada desde ningún
`SaveChanges`** — es código muerto o pendiente de conectar. En la
práctica, las filas de `AuditLog` que sí existen se crean manualmente
dentro de handlers concretos (confirmado en
`backend/Modules/Expenses/Application/Features/Expenses/Handlers/ExpenseCommandHandlers.cs`
y `backend/Modules/Billing/Application/Features/Billing/Handlers/BillingHandlers.cs`
con `new AuditLog { ... }`), no de forma automática y transversal. Ver
ADR-0017 (Audit Logs) para el detalle de qué operaciones quedan
efectivamente auditadas.

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

La dirección de dependencias prevista aquí (core → nada, módulos → core) se
incumple en la práctica: `backend/Erp.Infrastructure.csproj` referencia las
capas Application de 5 módulos (Inventory, Billing, Crm, Accounting,
Expenses), y `PgcSeeder.cs` (core) inyecta `IAccountingDbContext`. El patrón
de "ancla de assembly" para registrar `AddMediatR` por módulo en `Program.cs`
es frágil (Payroll no tiene el bloque porque no tiene handlers, y nadie lo
detectó) y depende de que exista al menos un tipo en cada módulo, lo que en
CRM llevó a mantener un archivo de handlers muertos solo para ese propósito
(ver ADR-0004). El patrón CQRS que aquí se describe como estándar se
incumple en 26 de 43 controllers de todo el backend (ver ADR-0018 §5 y §7).

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
- **No existe ningún proyecto de tests** en el repositorio (verificado:
  `find . -iname "*.Tests.csproj"` no devuelve resultados, y
  `frontend/package.json` no define ningún script de test ni depende de un
  test runner — solo `dev`, `build`, `start`, `lint`). Cualquier cambio en
  cualquier módulo se hace hoy sin red de seguridad de regresión
  automatizada; conviene verificar manualmente (build, smoke test del
  endpoint/página afectada) antes de dar un cambio por terminado.
- Si se decide activar el interceptor de auditoría automático, hacerlo de
  forma centralizada (p. ej. como `SaveChangesInterceptor` de EF Core
  registrado en cada `ModuleDbContextBase`) en vez de seguir añadiendo
  llamadas manuales `new AuditLog{...}` módulo a módulo.

## Consecuencias
- **`backend/Erp.slnx` no refleja todos los módulos activos.** El archivo
  de solución solo registra explícitamente los cuatro proyectos de
  Inventory, Billing y CRM (además de los cuatro núcleo). Sin embargo,
  `Program.cs` referencia y registra explícitamente además Accounting,
  Expenses, Treasury, Payroll, Purchasing y Sales — módulos que existen
  como carpetas de proyecto completas bajo `backend/Modules/` con su
  propio `.csproj`, y que compilan porque `Erp.Api.csproj` los referencia
  directamente (no depende del `.slnx` para resolver dependencias de
  proyecto). Esto es una **inconsistencia de tooling**: IDEs que abran la
  solución vía `Erp.slnx` (Visual Studio, Rider) no mostrarán seis de los
  nueve módulos de negocio, lo que puede llevar a desincronización al
  añadir archivos nuevos o a confusión sobre qué módulos existen. Conviene
  reconciliar `Erp.slnx` para que liste los cuatro proyectos de cada uno
  de los nueve módulos.
- El **outbox** garantiza entrega at-least-once pero no order-preserving
  entre distintos tipos de evento; los consumidores deben ser idempotentes.
- El interceptor de auditoría transversal (`AuditInterceptor.cs`) está
  definido pero no conectado — quien busque "por qué esta operación no
  generó AuditLog" debe revisar el handler específico, no asumir que hay
  auditoría automática.
- La ausencia total de tests automatizados (backend y frontend) es el
  mayor riesgo estructural del repositorio: cualquier refactor amplio
  (p. ej. tocar `ModuleDbContextBase` o el pipeline de MediatR) solo puede
  validarse manualmente o mediante build + smoke test.

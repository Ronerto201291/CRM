# ADR-0015: Automatización

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El nombre "motor de automatización" sugiere un sistema genérico de reglas
configurables por el usuario (condición → acción, disparadas por eventos
de negocio). El modelo de dominio (`Rule`/`Condition`/`Action`) está
diseñado exactamente para eso, pero **la implementación real no lo usa**:
existe un job de Hangfire con dos reglas de negocio **hardcodeadas en
código C#**, sin relación con las entidades `Rule`/`Condition`/`Action`.
Esta ADR documenta ambas piezas por separado porque, a fecha de esta
revisión, no están conectadas entre sí — es importante que quien trabaje
aquí no asuma que crear una fila `Rule` desde la UI tiene ningún efecto.

## Decisión

### Backend — modelo de dominio (no conectado a ejecución)
`backend/Erp.Domain/Entities/Automation/`:

- `Rule` (`CompanyId`, `Name`, `Description`, `TriggerEvent` —comentario:
  p. ej. `"OnInvoiceCreated"`, `"OnLeadStatusChanged"`—, `IsActive`,
  colecciones `Conditions`/`Actions`).
- `Condition` (`RuleId`, `Field`, `Operator`, `Value` — todo como texto
  libre, p. ej. `Field="Total"`, `Operator=">"`, `Value="1000"`).
- `Action` (`RuleId`, `Type` — p. ej. `"SendEmail"`, `"CreateTask"` —,
  `Configuration` JSONB, `ExecutionOrder`).

Las tres están mapeadas en `ErpDbContext` (`DbSet<Rule> Rules`,
`DbSet<Condition> Conditions`, `DbSet<Action> Actions`, esquema
`public`, con `HasQueryFilter` por `CompanyId` en `Rule`) y tienen
migración aplicada, así que las tablas existen en base de datos.

En `backend/Erp.Application/Features/Automation/Commands/CreateRuleCommand.cs`
hay un único comando, `CreateRuleCommand` + `CreateRuleHandler`, y **ni
siquiera él persiste la regla**: construye un objeto `Rule` en memoria,
pero nunca lo añade a un `DbSet` (el propio código lo admite en un
comentario: *"We need a DbSet for Rules - for now use direct access
through DbContext... Will be enhanced when we add Rules to
IApplicationDbContext"*) y llama a `SaveChangesAsync()` sin cambios
pendientes. No existe ningún controller (`AutomationController` o
similar) que exponga este comando por HTTP, ni queries para leer
`Rule`/`Condition`/`Action`.

### Backend — ejecución real: `RuleEvaluatorJob`
`backend/Erp.Infrastructure/Automation/RuleEvaluatorJob.cs` implementa
**dos reglas de negocio fijas en código**, sin leer las tablas
`Rule`/`Condition`/`Action`:

1. **Facturas vencidas** (`CheckOverdueInvoicesAsync`): busca en
   `IBillingDbContext.Invoices` (con `IgnoreQueryFilters`) facturas con
   `DueDate` pasada y `Status` distinto de `Paid`/`Cancelled`/`Draft`,
   las agrupa por `CompanyId` y envía un email de recordatorio de cobro
   al primer usuario activo de cada empresa (`IEmailService.SendAsync`).
2. **Stock bajo punto de reorden** (`CheckStockReorderPointsAsync`):
   busca en `IInventoryDbContext.InventoryProducts` productos con
   `ReorderPoint > 0`, suma el stock agregado de todos los almacenes
   (`IInventoryDbContext.Stocks`), y envía un email de alerta de
   reposición por empresa para los productos por debajo de su punto de
   reorden.

Ambas reglas son idempotentes por diseño (vuelven a evaluar el estado
actual en cada ejecución, no marcan filas como "ya notificadas"), y el
método público `EvaluateRulesAsync` tiene `[AutomaticRetry(Attempts = 3)]`
de Hangfire.

**Este job no está registrado como recurring job.** Una búsqueda de
`RecurringJob.AddOrUpdate<...RuleEvaluatorJob>` en todo el backend no
devuelve resultados — a diferencia de otros jobs sí registrados en
`Program.cs` (`OutboxProcessorJob`, `ExpireQuotesJob`,
`FiscalReminderJob`) o en `DependencyInjection.cs` de Accounting
(`AmortizationMonthlyJob`, `DeferredEntryMonthlyJob`). En el estado
actual del código, `RuleEvaluatorJob.EvaluateRulesAsync` no se ejecuta
nunca de forma automática; solo se ejecutaría si algo lo disparase
manualmente (p. ej. desde el dashboard de Hangfire en `/hangfire`, si se
registrara ahí como job puntual) o si se añadiera su registro.

### Frontend
`frontend/src/app/settings/automation/page.tsx` es una página **estática,
sin llamadas a la API**: no hay `fetch`, no hay `useState`/`useEffect`
cargando datos, y el botón "Crear Regla" no tiene manejador `onClick`. La
tabla que muestra ("Aviso Factura Grande", `OnInvoiceCreated`,
`Total > 10000`, "Enviar Email a Admin", "Activo") es un dato de ejemplo
escrito directamente en el JSX, no el resultado de ninguna consulta. Es,
en la práctica, un mockup visual del diseño previsto, no una pantalla
funcional.

### Modelo de datos
Esquema `public` (vía `ErpDbContext`): `Rule`, `Condition`, `Action` —
tablas existentes, con filtro de tenant en `Rule`, pero sin ningún
lector/escritor funcional en el resto del sistema.

### Flujo end-to-end representativo
No existe un flujo end-to-end real que conecte UI → regla configurable →
ejecución, porque ninguna de las tres capas (frontend, comando de alta,
motor de ejecución) está enlazada con las otras dos. El único flujo que
sí ocurre en código, si se registrara `RuleEvaluatorJob` como recurring
job, sería:

1. Hangfire dispara `EvaluateRulesAsync` en el intervalo configurado.
2. Se consultan facturas vencidas y stock bajo mínimos directamente
   contra `IBillingDbContext`/`IInventoryDbContext` (cross-módulo, sin
   pasar por outbox).
3. Se agrupa por `CompanyId`, se busca el usuario activo más antiguo de
   cada empresa como destinatario, y se envía un único email por regla y
   por empresa vía `IEmailService`.

## Relación con otros módulos
- `RuleEvaluatorJob` es el único punto del sistema que lee directamente
  de `IBillingDbContext` e `IInventoryDbContext` desde fuera de esos
  módulos (inyectados por interfaz), sin pasar por el patrón de outbox
  descrito en ADR-0001. Esto lo hace cross-cutting por diseño, pero
  **no** está conectado al outbox: no consume `OutboxMessage`, consulta
  directamente el estado actual de las tablas de otros módulos en cada
  ejecución.
- No hay ninguna relación de código entre este job y el modelo
  `Rule`/`Condition`/`Action` — pese a que conceptualmente ambos cubren
  "automatización", son dos implementaciones desconectadas.
- No se ha encontrado ningún publisher/handler que emita eventos hacia
  `Rule.TriggerEvent` (p. ej. `"OnInvoiceCreated"`); ese campo es, hoy,
  documentación de intención sin código que lo dispare.

## Buenas prácticas aplicables
- Antes de añadir una regla de negocio nueva del mismo tipo que las dos
  existentes (email condicionado a un estado agregado), el patrón a
  seguir es el de `RuleEvaluatorJob`: método idempotente, agrupado por
  `CompanyId`, con manejo de errores por email individual (`try/catch`
  alrededor de cada `SendAsync` para que un fallo de un tenant no
  bloquee al resto).
- Si se decide construir el motor genérico real, la integración natural
  con el resto de la arquitectura sería que los `INotificationHandler`
  que hoy publican eventos al outbox (ADR-0001) también evaluasen las
  `Rule.TriggerEvent` activas de ese tenant y encolasen las `Action`
  correspondientes — evitando así una segunda vía de lectura directa
  cross-módulo como la que usa `RuleEvaluatorJob` hoy.
- Cualquiera que quiera activar las dos reglas actuales en producción
  debe añadir explícitamente el `RecurringJob.AddOrUpdate<RuleEvaluatorJob>`
  en `Program.cs`, siguiendo el mismo patrón que `FiscalReminderJob` o
  `ExpireQuotesJob`.

## Consecuencias
- **El "motor de automatización" configurable por el usuario no existe
  todavía como funcionalidad**: el modelo de datos está preparado, pero
  no hay API para leer reglas, el único comando de alta no persiste
  nada, y el frontend es un mockup estático sin llamadas al backend.
  Cualquier trabajo futuro sobre "Automatización" parte, en la práctica,
  de cero en cuanto a lógica de ejecución genérica.
- Las dos reglas de negocio reales (facturas vencidas, stock bajo
  mínimo) están completamente implementadas pero **inertes**: no se
  ejecutan nunca porque el job no está registrado como recurring job en
  Hangfire. Activar la automatización descrita en el README/marketing
  del producto puede ser tan simple como añadir una línea de registro,
  pero hoy no ocurre.
- Quien deba diagnosticar "por qué no me llegó el aviso de factura
  vencida" debe saber que la causa más probable no es un bug en la
  lógica de `RuleEvaluatorJob`, sino que el job nunca se dispara.

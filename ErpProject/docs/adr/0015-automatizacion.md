# ADR-0015: Automatización

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El motor de automatización combina **reglas del sistema** (facturas vencidas,
stock bajo reorden) con **reglas configurables por el usuario**
(`Rule`/`Condition`/`Action` en esquema `public`). Tras la corrección del
backlog ADR-0018 #27, ambas vías están cableadas: API + frontend, job Hangfire
diario y evaluación en tiempo real para algunos eventos de dominio.

## Decisión

### Backend — modelo de dominio y API
`backend/Erp.Domain/Entities/Automation/`:

- `Rule` (`CompanyId`, `Name`, `Description`, `TriggerEvent` —p. ej.
  `"OnInvoiceCreated"`, `"OnLeadStatusChanged"`, `"OnInvoiceOverdue"`,
  `"OnStockBelowReorder"`—, `IsActive`, colecciones `Conditions`/`Actions`).
- `Condition` (`RuleId`, `Field`, `Operator`, `Value` — texto libre).
- `Action` (`RuleId`, `Type` — p. ej. `"SendEmail"`, `"NotifyAdmin"` —,
  `Configuration` JSONB, `ExecutionOrder`).

Mapeadas en `ErpDbContext` con `HasQueryFilter` por `CompanyId` en `Rule`.

`AutomationController` (`api/automation/rules`, `[Authorize]`) expone
`GET` (listado), `POST` (alta) y toggle de activación vía `IMediator`
(`GetRulesQuery`, `CreateRuleCommand`, `ToggleRuleCommand`).
`CreateRuleHandler` persiste `Rule` + `Condition` + `Action` en
`IApplicationDbContext.Rules`.

### Backend — ejecución
**Job programado:** `RuleEvaluatorJob`
(`backend/Erp.Infrastructure/Automation/RuleEvaluatorJob.cs`), registrado en
`Program.cs` como recurring job `rule-evaluator` (cron `0 9 * * *` — diario
9:00 UTC). **`ProactiveNotificationsJob`** (`Erp.Infrastructure/Jobs/`, cron
`0 8 * * *`, ADR-0018 #42) envuelve el evaluador + aprobaciones pendientes
(gastos `PendingApproval`, PO `PendingApproval`) respetando
`Company.ProactiveNotificationsFrequency` (`disabled|daily|weekly`). Ejecuta:

1. Reglas fijas en código: facturas vencidas (`CheckOverdueInvoicesAsync`) y
   stock bajo punto de reorden (`CheckStockReorderPointsAsync`), leyendo
   `IBillingDbContext`/`IInventoryDbContext` vía puertos
   (`IAutomationBillingQuery`, `IAutomationInventoryQuery` — ADR-0018 #13).
2. Reglas personalizadas de BD: `DatabaseRuleEvaluator` lee reglas activas,
   evalúa condiciones (`Field`/`Operator`/`Value`) y ejecuta acciones
   `SendEmail`/`NotifyAdmin`.

**Tiempo real:** `RealtimeRuleEvaluator` invocado desde
`LeadStatusChangedRuleHandler` y `ExpenseApprovedRuleHandler` al publicarse
esos eventos de dominio.

`RuleConditionEvaluator` centraliza la evaluación de condiciones. Las reglas
fijas del sistema son idempotentes por diseño (re-evalúan estado actual);
`EvaluateRulesAsync` tiene `[AutomaticRetry(Attempts = 3)]` de Hangfire.

### Frontend
`frontend/src/app/settings/automation/page.tsx` conectado al backend:
lista reglas vía `GET /api/proxy/automation/rules`, crea con `POST`, toggle
activo/inactivo, y muestra las reglas del sistema con su programación Hangfire.

### Modelo de datos
Esquema `public` (vía `ErpDbContext`): `Rule`, `Condition`, `Action` — con
filtro de tenant en `Rule`, lectura/escritura funcional vía API y evaluadores.

### Flujo end-to-end representativo
Alta de regla personalizada + ejecución diaria:

1. El usuario crea una regla en `settings/automation` →
   `POST /api/automation/rules` → `CreateRuleHandler` persiste en BD.
2. A las 9:00 UTC, Hangfire dispara `RuleEvaluatorJob.EvaluateRulesAsync`.
3. `DatabaseRuleEvaluator` carga reglas activas del tenant (con
   `IgnoreQueryFilters` para iterar empresas), evalúa condiciones y envía
   emails vía `IEmailService` cuando corresponde.
4. En paralelo, las reglas fijas (facturas vencidas, stock bajo) se ejecutan
   como respaldo del mismo job.

Flujo en tiempo real (lead):

1. CRM publica `LeadStatusChangedEvent`.
2. `LeadStatusChangedRuleHandler` invoca `RealtimeRuleEvaluator` con el
   evento y las reglas activas cuyo `TriggerEvent` coincide.
3. Si las condiciones se cumplen, se ejecuta la acción configurada.

## Relación con otros módulos
- **Billing / Inventory:** el job lee estado agregado vía puertos en
  `Erp.Application` (`IAutomationBillingQuery`, `IAutomationInventoryQuery`),
  no referencias directas a `*.Application` de módulos desde
  `Erp.Infrastructure` (ADR-0018 #13).
- **CRM / Expenses:** triggers en tiempo real vía handlers de infraestructura
  que escuchan eventos MediatR publicados por esos módulos.
- **ADR-0001 (Outbox):** la automatización no consume `OutboxMessage`; consulta
  estado actual o reacciona a notificaciones MediatR en memoria. Es un patrón
  cross-cutting deliberado, distinto del outbox transaccional.

## Evaluación de calidad arquitectónica
> Metodología completa en `ADR-0018`.

- **Controllers delgados:** `AutomationController` solo `IMediator` ✅.
- **CQRS:** comandos/queries en `Erp.Application/Features/Automation/` ✅.
- **DIP / dirección de dependencias:** lectura cross-módulo vía puertos en core
  (#13) ✅.
- **Pendiente:** `OnInvoiceCreated` en tiempo real sigue cubierto principalmente
  por el job diario, no por un handler event-driven dedicado; ampliar triggers
  siguiendo el patrón de `LeadStatusChangedRuleHandler`.

## Buenas prácticas aplicables
- Al añadir reglas del sistema (email condicionado a estado agregado), seguir
  el patrón de `RuleEvaluatorJob`: método idempotente, agrupado por
  `CompanyId`, `try/catch` por email individual para no bloquear otros tenants.
- Para triggers en tiempo real, registrar un `INotificationHandler<TEvent>`
  que delegue en `RealtimeRuleEvaluator` — no duplicar lógica de evaluación.
- Nuevas reglas configurables deben usar `TriggerEvent` coherente con los
  valores que `DatabaseRuleEvaluator`/`RealtimeRuleEvaluator` reconocen.

## Consecuencias
- **Corregido (#27):** el motor configurable está operativo (API, persistencia,
  frontend, job Hangfire). Las reglas del sistema y las de BD comparten el
  mismo job diario.
- Los emails dependen de `IEmailService` configurado (SMTP); sin SMTP válido,
  la lógica se ejecuta pero el envío falla (registrado en log por tenant).
- Quien diagnostique "no me llegó el aviso" debe comprobar: regla activa,
  condiciones, SMTP, y que el job `rule-evaluator` aparece en Hangfire Dashboard.
- Ampliar automatización (p. ej. `OnInvoiceCreated` en tiempo real) reutiliza
  la infraestructura existente; no requiere rehacer el modelo `Rule`.

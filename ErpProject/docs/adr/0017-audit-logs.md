# ADR-0017: Audit Logs

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El README describe un `AuditLog` inmutable con `OldValues`/`NewValues`
en JSONB y hash de integridad, obligatorio por la Ley 11/2021 Antifraude.
La entidad y su lectura vía API existen tal cual, pero **la escritura no
es automática**: existe un interceptor (`AuditInterceptor`) diseñado para
generar filas `AuditLog` a partir del `ChangeTracker` de cualquier
`DbContext`, pero no está conectado a ningún `SaveChangesAsync` — ni al
del `DbContext` core (`ErpDbContext`) ni al de la clase base que heredan
todos los DbContext de módulo (`ModuleDbContextBase`, ADR-0001). Esta ADR
documenta qué hay realmente wireado hoy: registro **manual**, hecho a
propósito dentro de un puñado de handlers concretos, no un mecanismo
transversal automático.

## Decisión

### Backend
`AuditLog` (`backend/Erp.Domain/Entities/Audit/AuditLog.cs`, esquema
`public`, vía `ErpDbContext`): `CompanyId`, `UserId`, `Entity`
(nombre de tipo), `EntityId?`, `Action`, `Timestamp`, `OldValues`/`NewValues`
(JSONB, por defecto `"{}"`), y `Hash?` opcional
(`SHA256(Entity|EntityId|Action|Timestamp|OldValues|NewValues)`, según el
comentario del entity, aunque el cálculo real del hash lo hace cada
llamador, no la entidad).

**Lectura:** `AuditLogsController`
(`backend/Erp.Api/Controllers/AuditLogsController.cs`, ruta
`api/AuditLogs`, `[Authorize]`) expone `GET` con filtros `action`,
`entityType`, `dateFrom`, `dateTo` y paginación (`page`, `pageSize`),
delegando en `GetAuditLogsQuery`/`GetAuditLogsHandler`
(`backend/Erp.Application/Features/AuditLogs/`).

**Escritura — interceptor definido pero no conectado:**
`AuditInterceptor.ProcessAuditEntries` (`backend/Erp.Infrastructure/Interceptors/AuditInterceptor.cs`)
es un método estático que recorre `ChangeTracker.Entries()` de un
`IApplicationDbContext` y construye una fila `AuditLog` por cada entidad
`Added`/`Modified`/`Deleted`, serializando `OriginalValues`/`CurrentValues`
a JSON. Una búsqueda de `ProcessAuditEntries`/`AuditInterceptor` en todo
el backend solo devuelve su propia definición: **no se invoca desde
ningún `SaveChangesAsync`**, ni está registrado como
`ISaveChangesInterceptor` de EF Core en `ErpDbContext` ni en
`ModuleDbContextBase`
(`backend/Erp.Infrastructure/Data/ModuleDbContextBase.cs`, que solo
gestiona `CreatedAt`/`UpdatedAt` y asignación de `CompanyId`). Es, por
tanto, **código muerto o pendiente de conectar**.

**Escritura — lo que realmente ocurre:** un puñado de handlers concretos
insertan `AuditLog` a mano, en la misma transacción que la operación de
negocio, con el mismo patrón:

```csharp
var auditEntry = new AuditLog { Id = ..., CompanyId = ..., UserId = ...,
    Entity = "Invoice", EntityId = inv.Id, Action = "Locked",
    Timestamp = DateTime.UtcNow,
    OldValues = JsonSerializer.Serialize(new { IsLocked = false }),
    NewValues = JsonSerializer.Serialize(new { IsLocked = true, inv.Hash }) };
auditEntry.Hash = Convert.ToHexStringLower(SHA256.HashData(
    Encoding.UTF8.GetBytes($"{Entity}|{EntityId}|{Action}|{Timestamp:O}")));
_appCtx.AuditLogs.Add(auditEntry);
```

Confirmado en:
- `backend/Modules/Billing/Application/Features/Billing/Handlers/BillingHandlers.cs`
  (bloqueo/contabilización de facturas, con hash calculado).
- `backend/Modules/Expenses/Application/Features/Expenses/Handlers/ExpenseCommandHandlers.cs`
  (aprobación/rechazo de gastos).

No se ha encontrado ningún otro handler de los nueve módulos de negocio
que escriba `AuditLog`. Es decir: altas de clientes, cambios de stock,
asientos contables manuales, cambios de nómina, etc. **no generan
auditoría** salvo que un desarrollador la haya añadido explícitamente
para ese caso concreto, como se hizo en Billing y Expenses.

### Frontend
`frontend/src/app/settings/audit-logs/page.tsx`: página real conectada a
la API (`GET /api/proxy/audit-logs` con query params de filtro:
`action`, `entityType`, `dateFrom`/`dateTo` por defecto últimos 30 días),
con tabla de resultados e iconos por tipo de entidad. A diferencia de la
página de Automatización (ADR-0015), esta sí hace fetch real y reacciona
a cambios de filtro.

### Modelo de datos
Esquema `public` (`ErpDbContext`): tabla `AuditLog` única, sin
particionar por módulo — cualquier handler de cualquier módulo que
quiera auditar debe inyectar `IApplicationDbContext` (el core, no su
propio `IXxxDbContext` de módulo) para acceder a `AuditLogs`.

### Flujo end-to-end representativo
Bloqueo de una factura (el caso más completo observado):

1. El handler de `LockInvoiceCommand` en Billing marca la factura como
   bloqueada (`IsLocked = true`) y calcula su hash de encadenamiento
   fiscal (`Invoice.Hash`, ver ADR-0013).
2. Tras `SaveChangesAsync` de la operación de negocio, el mismo handler
   construye manualmente un `AuditLog` con `Entity = "Invoice"`,
   `Action = "Locked"`, `OldValues`/`NewValues` describiendo el cambio de
   `IsLocked`, calcula su propio `Hash` con SHA-256 y lo guarda con un
   segundo `SaveChangesAsync` sobre `IApplicationDbContext`.
3. Un usuario con acceso a `/settings/audit-logs` puede filtrar por
   `entityType=Invoice` y `action=Locked` para ver esa entrada.

## Relación con otros módulos
- **Cross-cutting por diseño** (la tabla es única y compartida), pero
  **no cross-cutting en la práctica**: solo Billing y Expenses la
  alimentan hoy. Cualquier otro módulo (CRM, Accounting, Inventory,
  Payroll, Purchasing, Sales, Treasury) no deja rastro en `AuditLog`
  salvo que se le añada el mismo patrón manual.
- **ADR-0001 (arquitectura general):** ya documenta este mismo hallazgo
  a nivel de patrón compartido (el interceptor no conectado); esta ADR
  detalla el alcance real módulo por módulo.
- **ADR-0013 (Fiscal/SII/VeriFactu):** el caso de Billing muestra que
  cuando la auditoría sí importa por motivos legales (encadenamiento de
  hash de facturas, Ley Antifraude), se ha implementado manualmente en
  el punto crítico, en vez de depender del interceptor genérico.

## Buenas prácticas aplicables
- **No asumir que una operación queda auditada por el mero hecho de
  pasar por un `DbContext`.** Si una operación de negocio necesita
  trazabilidad legal o de soporte, hay que añadir explícitamente el
  bloque `new AuditLog {...}` + `_appCtx.AuditLogs.Add(...)` en el
  handler correspondiente, siguiendo el patrón de Billing/Expenses.
- Si se decide activar la auditoría automática, el punto de conexión
  correcto es un `ISaveChangesInterceptor` de EF Core registrado tanto
  en `ErpDbContext` como en `ModuleDbContextBase` (para cubrir los nueve
  módulos a la vez con una sola pieza de código), no seguir añadiendo
  llamadas manuales módulo a módulo — así lo recomienda también
  ADR-0001.
- Al escribir `AuditLog` manualmente, mantener el mismo formato de hash
  (`SHA256(Entity|EntityId|Action|Timestamp:O|...)`) usado en Billing
  para que las entradas sean comparables/verificables de la misma forma
  si en el futuro se audita la integridad del log.

## Consecuencias
- El sistema de auditoría, tal como está hoy, **cubre una fracción
  pequeña y deliberadamente elegida** de las operaciones del ERP
  (bloqueo de facturas, aprobación/rechazo de gastos), no todas las
  escrituras como sugeriría un interceptor transversal. Cualquier
  requisito de cumplimiento que asuma "todo cambio queda auditado" no
  está satisfecho por el código actual.
- El coste de activar el interceptor real es relativamente bajo (ya
  existe la lógica de recorrido del `ChangeTracker` en
  `AuditInterceptor.ProcessAuditEntries`; falta conectarlo como
  interceptor de EF Core y decidir qué entidades excluir, p. ej. el
  propio `AuditLog` para evitar recursión), pero mientras no se haga,
  añadir auditoría a un módulo nuevo requiere trabajo manual explícito
  módulo a módulo.
- Al no haber un interceptor centralizado, no hay garantía de
  consistencia en el formato de `Entity`/`Action` entre los pocos sitios
  que sí auditan (p. ej. `"Locked"` en Billing es un `Action` libre, no
  una enumeración compartida); un futuro trabajo de consolidación
  debería fijar un vocabulario común antes de generalizar la escritura.

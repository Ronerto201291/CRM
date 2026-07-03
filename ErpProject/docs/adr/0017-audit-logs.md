# ADR-0017: Audit Logs

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El README describe un `AuditLog` inmutable con `OldValues`/`NewValues`
en JSONB y hash de integridad, obligatorio por la Ley 11/2021 Antifraude.
La entidad y su lectura vía API existen tal cual. **La escritura automática
está conectada** desde ADR-0018 #31: `AuditSaveChangesInterceptor` en
`ErpDbContext` y en los nueve DbContext de módulo. Algunos handlers fiscales
(Billing, Expenses) además escriben entradas manuales con formato/hash
específico en puntos críticos.

## Decisión

### Backend
`AuditLog` (`backend/Erp.Domain/Entities/Audit/AuditLog.cs`, esquema
`public`, vía `ErpDbContext`): `CompanyId`, `UserId`, `Entity`
(nombre de tipo), `EntityId?`, `Action`, `Timestamp`, `OldValues`/`NewValues`
(JSONB, por defecto `"{}"`), y `Hash?` opcional
(`SHA256(Entity|EntityId|Action|Timestamp|OldValues|NewValues)`).

**Lectura:** `AuditLogsController`
(`backend/Erp.Api/Controllers/AuditLogsController.cs`, ruta
`api/AuditLogs`, `[Authorize]`) expone `GET` con filtros `action`,
`entityType`, `dateFrom`, `dateTo` y paginación (`page`, `pageSize`),
delegando en `GetAuditLogsQuery`/`GetAuditLogsHandler`
(`backend/Erp.Application/Features/AuditLogs/`).

**Escritura automática (activa):**
`AuditSaveChangesInterceptor`
(`backend/Erp.Infrastructure/Interceptors/AuditSaveChangesInterceptor.cs`)
implementa `SaveChangesInterceptor` de EF Core. Recorre
`ChangeTracker.Entries()` en `SavingChangesAsync`, construye una fila
`AuditLog` por cada entidad `Added`/`Modified`/`Deleted` (excluyendo
entidades de sistema como el propio `AuditLog`), serializa valores
anterior/nuevo a JSON y calcula hash SHA-256. Registrado en:

- `ErpDbContext` (`Erp.Infrastructure/DependencyInjection.cs`)
- Cada `*DbContext` de módulo (p. ej. `AddAccountingInfrastructure`,
  `AddBillingInfrastructure`, … — nueve módulos)

La clase estática `AuditInterceptor.ProcessAuditEntries` es código legado
no registrado; no confundir con el interceptor activo.

**Escritura manual complementaria:** handlers de Billing y Expenses insertan
`AuditLog` adicional en operaciones fiscales críticas (bloqueo de factura,
aprobación/rechazo de gasto) con el mismo patrón de hash, además del
registro que genera el interceptor por el cambio de entidad.

### Frontend
`frontend/src/app/settings/audit-logs/page.tsx`: página conectada a la API
(`GET /api/proxy/audit-logs` con filtros), tabla de resultados e iconos por
tipo de entidad. Muestra entradas generadas por el interceptor automático.

### Modelo de datos
Esquema `public` (`ErpDbContext`): tabla `AuditLog` única, compartida por
todos los módulos. El interceptor escribe en `IApplicationDbContext.AuditLogs`
desde cualquier DbContext que lo tenga registrado.

### Flujo end-to-end representativo
Modificación de un cliente en CRM:

1. `UpdateClientCommand` persiste cambios en `CrmDbContext`.
2. En `SaveChangesAsync`, `AuditSaveChangesInterceptor` detecta la entidad
   `Client` en estado `Modified`, serializa `OldValues`/`NewValues` y
   añade una fila `AuditLog` con hash SHA-256.
3. El usuario consulta `/settings/audit-logs`, filtra por
   `entityType=Client`, y ve la entrada.

Bloqueo de factura (caso fiscal con doble trazabilidad):

1. `LockInvoiceHandler` marca `IsLocked = true` y calcula hash de factura.
2. El interceptor registra el cambio de `Invoice`; el handler además escribe
   un `AuditLog` manual con `Action = "Locked"` y detalle del hash fiscal.
3. Ambas entradas son consultables desde la UI de audit logs.

## Relación con otros módulos
- **Cross-cutting:** cualquier módulo cuyo DbContext registre
  `AuditSaveChangesInterceptor` deja rastro automático en `AuditLog`.
- **ADR-0001:** documenta el patrón de interceptor compartido; este ADR
  detalla entidad, API de lectura y el interceptor activo (#31).
- **ADR-0013 (Fiscal):** Billing mantiene auditoría manual explícita en
  bloqueo de facturas (encadenamiento antifraude), complementaria al
  interceptor genérico.

## Evaluación de calidad arquitectónica
> Metodología en `ADR-0018`.

- **Transversalidad:** ✅ interceptor en core + 9 módulos (#31).
- **Integridad:** hash SHA-256 en cada entrada del interceptor.
- **Pendiente:** vocabulario de `Entity`/`Action` no está centralizado en
  enumeración; posible inconsistencia entre interceptor y entradas manuales.
- **Pendiente:** no hay API de verificación de cadena de hashes del log
  completo (solo por entrada).

## Buenas prácticas aplicables
- Asumir que **toda** persistencia vía DbContext con interceptor registrado
  genera `AuditLog`, salvo entidades excluidas en el interceptor.
- Para operaciones fiscales que requieran `Action` o payload específico
  (p. ej. `"Locked"` con hash de factura), mantener el patrón manual de
  Billing/Expenses además del registro automático.
- Al añadir un DbContext nuevo de módulo, registrar `AuditSaveChangesInterceptor`
  en su `DependencyInjection` — mismo checklist que timestamps/tenant.

## Consecuencias
- **Corregido (#31):** el sistema ya no depende solo de dos handlers manuales;
  la mayoría de escrituras quedan auditadas automáticamente.
- El volumen de `AuditLog` crecerá con cada `SaveChanges`; conviene política
  de retención/archivado en producción (no implementada hoy).
- Entradas manuales y automáticas pueden coexistir para la misma operación
  (p. ej. bloqueo de factura) — no es un bug, es diseño dual fiscal/genérico.
- Quien depure "no veo el audit log" debe verificar: interceptor registrado
  en ese DbContext, entidad no excluida, y filtros de la UI (`entityType`).

# ADR-0009: Payroll (Nóminas)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Payroll gestiona trabajadores y liquidaciones mensuales de
nómina (bases de cotización TGSS, retención IRPF, líquido a percibir), y
genera exportes CSV/XML orientativos para TC1/TC2 destinados a asesoría o
importación en SILTRA/RED. Sigue la estructura estándar de módulo descrita
en ADR-0001 (`Api/Application/Domain/Infrastructure`), con `PayrollDbContext`
sobre el esquema `payroll`.

De los nueve módulos de negocio (CRM, Billing, Accounting, Expenses,
Inventory, Payroll, Purchasing, Sales, Treasury), **Payroll es el que
tiene menor superficie**: un único controller
(`backend/Modules/Payroll/Api/Controllers/PayrollController.cs`) con 8
endpoints, frente a los varios controllers y decenas de comandos/queries
de módulos como Billing o Accounting. No hay carpeta
`Application/Commands` ni `Application/Queries` con handlers de MediatR:
el controller accede directamente a `IPayrollDbContext` (patrón
"controller-repositorio" en vez de CQRS/MediatR), algo distinto al resto
de módulos documentados hasta ahora. El propio frontend rotula la página
como **"Nóminas (Fase 0)"**, lo que confirma que se trata de un
alcance inicial deliberado y no de un olvido de documentación.

Además, el `Domain/Entities` del módulo define seis entidades
(`Employee`, `PayrollSettlement`, `PayrollLine`, `Payroll`,
`PayrollDeduction`, `SocialSecurityContribution`, `TaxableBase`,
`PayrollTemplate`), pero **solo tres se usan realmente** en el
controller y en el frontend: `Employee`, `PayrollSettlement` y
`PayrollLine`. Las otras cuatro (`Payroll`, `PayrollDeduction`,
`SocialSecurityContribution`, `TaxableBase`, `PayrollTemplate`) están
mapeadas en `PayrollDbContext` (tienen `DbSet<T>`, filtro de tenant y
migración aplicada) pero ningún controller ni handler las lee o escribe:
son modelo de datos preparado para una fase futura (nómina detallada por
conceptos, plantillas de nómina, remisión a AEAT de bases 111/190) que
aún no tiene API ni UI.

## Decisión

### Backend
`PayrollController` (`backend/Modules/Payroll/Api/Controllers/PayrollController.cs`,
ruta base `api/payroll`, `[Authorize]`) expone:

- `GET/POST /api/payroll/employees` — alta y listado de trabajadores
  (`Employee`: `TaxId`, `FullName`, `SocialSecurityNumber` (NAF),
  `HireDate`, `ContractType`, `WeeklyHours`).
- `GET/POST /api/payroll/settlements` — liquidaciones mensuales
  (`PayrollSettlement`: `Year`, `Month`, `Status` Draft/Final), únicas por
  `(CompanyId, Year, Month)`.
- `POST /api/payroll/settlements/{id}/lines` — añade una `PayrollLine`
  por trabajador a una liquidación en borrador (bruto, base de
  contingencias comunes, cuota obrera/patronal SS, base/tipo/retención
  IRPF, líquido). Un empleado no puede tener dos líneas en la misma
  liquidación.
- `POST /api/payroll/settlements/{id}/finalize` — cierra la liquidación
  (`Draft` → `Final`) y genera el asiento contable correspondiente
  (ver "Relación con otros módulos").
- `GET /api/payroll/export/tc1`, `/export/tc2`,
  `/export/tc-red-orientativo` — exportan CSV (TC1: bases y cuotas por
  trabajador; TC2: retenciones IRPF y líquido) y un XML orientativo,
  solo para liquidaciones en estado `Final`. Todas las respuestas se
  marcan explícitamente como **no oficiales** vía
  `FiscalExportHeaders.MarkAsNonOfficial`, con el mismo mecanismo de
  cabeceras de aviso usado en otros exportes fiscales del ERP (ver
  ADR-0013).

No hay endpoints para editar/borrar trabajadores o líneas, ni para
reabrir una liquidación `Final`.

### Frontend
`frontend/src/app/payroll/page.tsx` es una única página (sin subrutas)
que consume directamente los 8 endpoints anteriores vía
`/api/proxy/payroll/...`: formulario de alta de empleado, tabla de
empleados activos, alta/listado de liquidaciones mensuales por año,
formulario manual de línea de nómina (los importes se introducen a mano,
no se calculan en el cliente ni en el servidor a partir de un convenio o
tabla de retenciones), botón "Finalizar" por liquidación en borrador, y
descarga de TC1/TC2. El título de la página es literalmente
"Nóminas (Fase 0)".

### Modelo de datos
Esquema `payroll` (PostgreSQL), vía `PayrollDbContext`
(`ModuleDbContextBase`):

- `Employee` (`CompanyId`, `TaxId`, `FullName`, `SocialSecurityNumber`,
  `HireDate`, `ContractType`, `WeeklyHours`, `IsActive`).
- `PayrollSettlement` (`CompanyId`, `Year`, `Month`, `Status`,
  `JournalEntryId?`) — único índice `(CompanyId, Year, Month)`.
- `PayrollLine` (`PayrollSettlementId` → `PayrollSettlement` con cascade
  delete, `EmployeeId` → `Employee` con `Restrict`, importes con
  precisión `(18,4)`).
- Entidades mapeadas pero sin uso desde API/UI:
  `Payroll`/`PayrollDeduction`/`SocialSecurityContribution`/`TaxableBase`/`PayrollTemplate`.

Todos los filtros de tenant (`HasQueryFilter`) son por `CompanyId`,
salvo `PayrollLine`, que se filtra indirectamente vía
`Settlement.CompanyId` porque no tiene `CompanyId` propio.

### Flujo end-to-end representativo
Cierre de una liquidación mensual y su reflejo contable:

1. El frontend crea la liquidación (`POST /settlements`) y añade una
   `PayrollLine` por trabajador con los importes ya calculados
   manualmente fuera del sistema (asesoría externa, hoja de cálculo,
   etc.).
2. Al pulsar "Finalizar", `POST /settlements/{id}/finalize` suma bruto,
   SS empresa, SS trabajador, IRPF retenido y líquido de todas las
   líneas, y llama a
   `AccountingService.GenerateEntryFromPayrollSettlement(...)`
   (`backend/Modules/Accounting/Application/Services/AccountingService.cs`),
   pasando esos totales y la fecha de devengo (día 1 del mes).
3. `AccountingService` genera el asiento contable (cuentas 640/642/476/4751/465
   según el comentario en `PayrollSettlement.JournalEntryId`) en el
   esquema `accounting`, y devuelve su `Id`.
4. El controller marca `Status = "Final"` y guarda `JournalEntryId` en la
   liquidación — llamadas repetidas a `finalize` sobre una liquidación ya
   cerrada devuelven el mismo `journalEntryId` sin duplicar el asiento
   (comprobación explícita en el controller).
5. Tras finalizar, el usuario puede descargar TC1/TC2 para esa
   liquidación desde la UI.

## Relación con otros módulos
- **Accounting (ADR-0006):** la única integración cross-módulo real del
  módulo es esta — `PayrollController.Finalize` invoca
  `AccountingService.GenerateEntryFromPayrollSettlement` directamente
  (llamada a servicio, no vía outbox/eventos como en el resto de
  integraciones entre módulos descritas en ADR-0001). Es una dependencia
  explícita de compilación de `Erp.Modules.Payroll.Api` sobre
  `Erp.Modules.Accounting.Application`, distinta del patrón de outbox
  transaccional usado por CRM o Billing.
- No hay integración con Expenses, Treasury ni con el motor de
  automatización (ADR-0015): no se ha encontrado ningún trigger de
  nómina en `RuleEvaluatorJob` ni en el resto del backend.
- No hay export SII/VeriFactu (ADR-0013) porque la nómina no genera
  facturas.

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

Payroll es el único módulo sin CQRS: `Application/` solo tiene
`Interfaces/IPayrollDbContext.cs`, sin `Features`/`Commands`/`Queries`/`Handlers`,
y `Program.cs` no registra ningún `AddMediatR` para este módulo. El único
`PayrollController` inyecta `IPayrollDbContext` y hasta
`AccountingService` (de otro módulo) directamente, con toda la lógica de
altas, liquidaciones y exportación TC1/TC2/RED implementada inline en el
controller — incumple tanto CQRS como "controllers delgados" en su forma más
extrema dentro del backend. Cualquier trabajo nuevo en este módulo debería
plantear migrar a CQRS en vez de seguir ampliando el controller.

## Buenas prácticas aplicables
- Cualquier extensión de este módulo (p. ej. activar
  `PayrollDeduction`/`SocialSecurityContribution`/`TaxableBase`/`PayrollTemplate`)
  debería adoptar el patrón CQRS/MediatR + `Application/Commands`,
  `Application/Queries` que usan el resto de módulos (ADR-0001), en vez
  de seguir añadiendo lógica directamente al controller.
- Los exportes TC1/TC2/XML deben seguir marcándose como no oficiales con
  `FiscalExportHeaders.MarkAsNonOfficial` mientras no se implemente
  generación del fichero RED/SILTRA real firmado.
- Antes de dar por completo el módulo, conviene decidir explícitamente
  si las entidades no usadas (`Payroll`, `PayrollDeduction`, etc.) se
  retiran del modelo o se conectan a una futura fase 1, para evitar
  tablas fantasma en el esquema `payroll`.

## Consecuencias
- El módulo es honestamente un **scaffold funcional de fase 0**: cubre
  el caso de uso mínimo (alta manual de trabajador, liquidación manual
  línea a línea, cierre con asiento contable, export CSV/XML
  orientativo), pero no calcula nóminas a partir de convenio, categoría
  o tablas de IRPF/SS — todos los importes de cada `PayrollLine` los
  introduce el usuario a mano. No hay gestión de bajas médicas,
  vacaciones, finiquitos, ni generación del recibo de salario en PDF.
- La ausencia de capa CQRS/MediatR en este módulo (a diferencia de los
  otros ocho) es una inconsistencia arquitectónica respecto a ADR-0001;
  quien trabaje aquí debe tenerlo presente y no asumir que existen
  `Commands`/`Queries` como en Billing o CRM.
- El acoplamiento directo a `AccountingService` (en vez de outbox) hace
  que `Erp.Modules.Payroll.Api` dependa en tiempo de compilación de
  `Erp.Modules.Accounting.Application`; un cambio de firma en
  `GenerateEntryFromPayrollSettlement` rompe Payroll de inmediato, sin
  el desacoplamiento que da el patrón de eventos usado en el resto del
  sistema.

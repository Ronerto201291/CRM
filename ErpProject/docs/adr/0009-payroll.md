# ADR-0009: Payroll (Nóminas)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Payroll gestiona trabajadores y liquidaciones mensuales de
nómina (bases de cotización TGSS, retención IRPF, líquido a percibir), y
genera exportes CSV/XML orientativos para TC1/TC2 y fichero RED texto plano
(ISO-8859-1) orientativo para asesoría o conciliación — **no homologado TGSS**
(ver `docs/payroll-red-siltra.md`). Sigue la estructura estándar de módulo descrita
en ADR-0001 (`Api/Application/Domain/Infrastructure`), con `PayrollDbContext`
sobre el esquema `payroll`.

De los nueve módulos de negocio (CRM, Billing, Accounting, Expenses,
Inventory, Payroll, Purchasing, Sales, Treasury), **Payroll es el que
tiene menor superficie**: un único controller
(`backend/Modules/Payroll/Api/Controllers/PayrollController.cs`) con 8
endpoints, frente a los varios controllers y decenas de comandos/queries
de módulos como Billing o Accounting. Todo el flujo expuesto por
`PayrollController` delega en MediatR (`Application/Features/{Employees,Settlements,Exports}/`);
`Program.cs` registra `AddMediatR` para el assembly de Payroll (backlog #10 ✅).
El propio frontend rotula la página
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

- `GET/POST /api/payroll/employees` — vía `GetEmployeesQuery` / `CreateEmployeeCommand`.
- `GET/POST /api/payroll/settlements`, `POST .../lines`, `POST .../finalize` —
  vía handlers en `Application/Features/Settlements/SettlementHandlers.cs`.
- `GET /api/payroll/export/tc1`, `/export/tc2`,
  `/export/tc-red-orientativo`, `/export/red` — vía handlers en
  `Application/Features/Exports/PayrollExportHandlers.cs` (`RedSiltraFileBuilder`
  para RED; validación NAF/CCC en `PayrollRedExportValidator`).

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
2. Al pulsar "Finalizar", `POST /settlements/{id}/finalize` despacha
   `FinalizeSettlementCommand`, que suma bruto, SS empresa, SS trabajador,
   IRPF retenido y líquido de todas las líneas, y llama a
   `IPayrollJournalEntryGenerator.GenerateFromPayrollSettlementAsync(...)`
   (puerto en `Erp.Application`, implementado en Accounting.Infrastructure —
   ADR-0018 #19c), pasando totales y fecha de devengo (día 1 del mes).
3. El generador crea el asiento contable (cuentas 640/642/476/4751/465) en el
   esquema `accounting` y devuelve su `Id`.
4. El handler marca `Status = "Final"` y guarda `JournalEntryId` en la
   liquidación — llamadas repetidas a `finalize` sobre una liquidación ya
   cerrada devuelven el mismo `journalEntryId` sin duplicar el asiento
   (comprobación en `FinalizeSettlementHandler`).
5. Tras finalizar, el usuario puede descargar TC1/TC2 para esa
   liquidación desde la UI.

## Relación con otros módulos
- **Accounting (ADR-0006):** integración vía puerto `IPayrollJournalEntryGenerator`
  (#19c) — `Payroll.Application` no referencia `Accounting.Application`
  directamente; la implementación vive en `Accounting.Infrastructure`.
- No hay integración con Expenses, Treasury ni con el motor de
  automatización (ADR-0015): no se ha encontrado ningún trigger de
  nómina en `RuleEvaluatorJob` ni en el resto del backend.
- No hay export SII/VeriFactu (ADR-0013) porque la nómina no genera
  facturas.

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

Payroll cumple CQRS en su superficie actual: `Features/Employees/`,
`Features/Settlements/` y `Features/Exports/` cubren los 8 endpoints.
`PayrollController` solo inyecta `IMediator` (controller delgado, backlog #10 ✅).
`FinalizeSettlementHandler` usa `IPayrollJournalEntryGenerator` (acoplamiento
resuelto vía puerto #19c).

## Buenas prácticas aplicables
- Cualquier extensión de este módulo (p. ej. activar
  `PayrollDeduction`/`SocialSecurityContribution`/`TaxableBase`/`PayrollTemplate`)
  debería adoptar el patrón CQRS/MediatR + `Application/Commands`,
  `Application/Queries` que usan el resto de módulos (ADR-0001), en vez
  de seguir añadiendo lógica directamente al controller.
- Los exportes TC1/TC2/XML/RED deben seguir marcándose como no oficiales con
  `FiscalExportHeaders.MarkAsNonOfficial`. El export RED (`RedSiltraFileBuilder`,
  `GET /api/payroll/export/red`) es texto plano ISO-8859-1 orientativo — **no**
  sustituye XML SILTRA homologado ni firma digital (véase `docs/payroll-red-siltra.md`).
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
- **Corregido (#10):** capa CQRS/MediatR completa para los 8 endpoints;
  `PayrollController` solo inyecta `IMediator`.
- La integración contable usa puerto `IPayrollJournalEntryGenerator` (#19c),
  no referencia directa a `Accounting.Application` desde Payroll.
- Exportes TC1/TC2/RED: orientativos sin homologación TGSS (#29 ✅ alcance máximo
  sin homologación — `RedSiltraFileBuilder`, validación NAF/CCC módulo 97,
  `PayrollRedExportValidator`, frontend botón RED; ver `docs/payroll-red-siltra.md`).

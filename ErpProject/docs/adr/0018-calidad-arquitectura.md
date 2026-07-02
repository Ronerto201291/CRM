# ADR-0018: Calidad arquitectónica transversal (SOLID, Clean Architecture, CQRS, duplicación, escalabilidad)

## Estado
Aceptado — auditoría del código real en `main` a fecha de este ADR. No es un
plan a futuro: cada hallazgo está verificado leyendo el archivo citado.

## Contexto
Los ADR 0001-0017 documentan la estructura de cada módulo, pero no evalúan
sistemáticamente su calidad arquitectónica. Este ADR cierra ese hueco con una
auditoría explícita en siete dimensiones — SOLID, Clean Architecture (capas y
dirección de dependencias), estructura de carpetas, código duplicado, código
limpio (en particular: ausencia de lógica de negocio en los controllers),
escalabilidad, y cumplimiento real de CQRS — y sirve de referencia única para
no repetir este análisis en cada ADR de módulo. A partir de este ADR, el
checklist que resume queda incorporado de forma permanente en
`0000-template.md`, para que cualquier ADR nuevo (módulo o revisión) se
evalúe siempre contra los mismos siete puntos.

## Decisión

### 1. SOLID

**SRP (Single Responsibility) — violado en puntos concretos:**
- `backend/Modules/Accounting/Api/Controllers/AccountingExportController.cs`
  (1362 líneas, 19 endpoints `[HttpGet]`) inyecta seis dependencias de
  contexto repartidas en cinco módulos distintos (`IAccountingDbContext`,
  `IExpensesDbContext`, `IApplicationDbContext`, `IBillingDbContext`,
  `ICrmDbContext`, `IPayrollDbContext`) y construye inline la exportación
  CSV/XML de ocho modelos fiscales AEAT (303, 111, 130, 190, 200, 202, 347,
  349, 390). Un único archivo mezcla routing, acceso a datos cross-módulo y
  serialización de formatos oficiales.
- `backend/Modules/Accounting/Application/Handlers/CloseFiscalYearHandler.cs`
  (487 líneas): un solo `Handle()` hace cierre de ejercicio fiscal, cálculo
  de P&L, generación de asientos de cierre y creación de saldos de apertura.
- Menos grave (organización de archivo, no de clase): `QuoteHandlers.cs`
  (Billing, 1011 líneas) y `TreasureHandlers.cs` (Treasury, 706 líneas)
  concatenan 11-14 handlers bien acotados en un único archivo — cada clase
  cumple SRP, el archivo no.

**OCP (Open/Closed):**
- `backend/Modules/Accounting/Application/Features/Vat/CalculateVatCommand.cs`
  resuelve tasas de IVA/recargo de equivalencia con un `Dictionary`/`switch`
  sobre literales de texto (`"Standard"`, `"Reduced"`, `"SuperReduced"`) —
  añadir una categoría fiscal nueva exige editar código, no datos.
- Contraejemplo positivo: `backend/Erp.Infrastructure/Security/ModuleAuthorizationHandler.cs`
  y `PermissionService.cs` resuelven gating de módulos y permisos 100%
  contra datos (`Plans`, `PlanModules`, `TenantModules`, `RolePermissions`) —
  ningún `switch` por nombre de módulo o permiso. Es el patrón a imitar.

**LSP (Liskov):**
- `backend/Modules/Accounting/Infrastructure/Data/AccountingDbContext.cs`
  sobrescribe `SaveChangesAsync` y añade precondiciones nuevas no anunciadas
  por el contrato base `ModuleDbContextBase` (lanza si el período fiscal está
  cerrado, o si Debe≠Haber). Es una decisión de negocio razonable (RD
  1619/2012), pero un consumidor que solo conozca `IAccountingDbContext` no
  tiene forma de saber que `SaveChangesAsync` puede lanzar por estas razones.
  Desviación pragmática, no un bug.

**ISP (Interface Segregation) — violado:**
- `IAccountingDbContext` expone **29 DbSets**; `ITreasuryDbContext`, 20;
  `IApplicationDbContext`, 17. Ejemplo concreto:
  `backend/Modules/Accounting/Application/Queries/GetFiscalPeriodsQuery.cs`
  inyecta el interface completo de 29 DbSets para usar únicamente
  `FiscalPeriods`. Cualquier cambio en la interfaz obliga a recompilar todos
  sus consumidores, incluidos los de otros módulos (Treasury).

**DIP (Dependency Inversion) — en general limpio:**
No se encontró `new HttpClient(...)`, uso de `DbContext` concreto, ni
service-locator dentro de ninguna capa Application (`Modules/*/Application`,
`Erp.Application`). La única grieta relevante es de dirección de módulos, no
de inversión de dependencias en sí (ver punto 2).

### 2. Clean Architecture y dirección de dependencias

**Violación grave: el core depende de los módulos, no al revés.**
`backend/Erp.Infrastructure/Erp.Infrastructure.csproj` referencia las capas
Application de cinco módulos (Inventory, Billing, Crm, Accounting, Expenses).
Síntoma concreto: `backend/Erp.Infrastructure/Seeding/PgcSeeder.cs` (código
"core") inyecta `IAccountingDbContext` (interfaz de módulo). La dirección
prevista en ADR-0001 es la contraria: los módulos dependen del core, el core
no conoce a los módulos.

**Acoplamiento cruzado sin abstracción compartida:**
Treasury → Accounting (`TreasureHandlers.cs`, `BankReconciliationService.cs`
inyectan `IAccountingDbContext` directamente), Payroll → Accounting
(`Payroll/Api` referencia `Accounting/Application`), Crm ↔ Expenses
(referencias cruzadas de `Application`). Ninguno pasa por un evento/outbox o
una interfaz común — son `ProjectReference` directos entre módulos que en
teoría deberían ser independientes.

**Dominio limpio (positivo):** cero dependencias de EF Core, Data Annotations
o ASP.NET encontradas en `Erp.Domain/` ni en ningún `Modules/*/Domain/`
(excluyendo `Migrations/`) — verificado en toda la base de código.

**Hallazgo nuevo — Inventory, más grave que el caso ya conocido de
Accounting:** `Product`, `Warehouse`, `Stock` y `StockMovement` viven
físicamente en `backend/Erp.Domain/Modules/Inventory/Entities/` (proyecto
core), no en `Modules/Inventory/Domain/` (que solo contiene `Lot.cs` y
existe casi únicamente para referenciar de vuelta a `Erp.Domain`). A
diferencia de `Account`/`JournalEntry` (Accounting), que al menos están
gateadas detrás de `IAccountingDbContext`, estas entidades de Inventory son
públicas en el core y están disponibles para cualquier módulo sin pasar por
ninguna interfaz.

### 3. Estructura de carpetas
7 de 9 módulos siguen el patrón `Api/Application/Domain/Infrastructure` con
cuatro `.csproj` separados (frontera real de compilador entre capas).
Desviaciones:
- **Inventory**: la carpeta es `Modules/Inventory/API` (mayúscula) en vez de
  `Api`; namespace `Erp.Modules.Inventory.API.Controllers`. Cosmético, no
  rompe la capa, pero es una inconsistencia real de nombres.
- **Sales y Purchasing**: un único `.csproj` (`Erp.Modules.Sales.Infrastructure.csproj`,
  `Erp.Modules.Purchasing.Infrastructure.csproj`) compila las cuatro carpetas
  lógicas como un solo assembly. Las carpetas `Api/Application/Domain/Infrastructure`
  existen pero son solo organizativas — nada impide en el compilador que
  `Domain` referencie `Infrastructure` dentro de esos dos módulos, a
  diferencia de los otros 7. Purchasing además tiene `Migrations/` en la
  raíz del módulo en vez de bajo `Infrastructure/Migrations/`.

### 4. Código duplicado
- **El mismo patrón corregido en VIES (ver ADR-0006) se repite sin corregir
  en VAT**: `backend/Modules/Accounting/Api/Controllers/VatController.cs`
  tiene su propio diccionario de tasas de IVA y cálculo inline, totalmente
  independiente de `CalculateVatCommand.cs` (que tiene otro diccionario con
  las mismas tasas más lógica real de ISP/intracomunitario/recargo y
  persistencia en `VatTransactions`). Ninguno de los dos llama al otro.
- `backend/Modules/Crm/Application/Handlers/ClientHandlers.cs`: set completo
  de queries/commands (`GetClientsModuleQuery`, `CreateClientModuleCommand`,
  etc.) que ningún controller referencia — el path real es
  `Application/Features/Crm/*`. Particularidad: `Program.cs` usa un tipo de
  este archivo como "ancla" de assembly para registrar MediatR de todo el
  módulo CRM, así que el archivo es código muerto pero estructuralmente
  necesario tal cual está montado hoy.
- Guard clauses de `tenantId` (`_tenant.TenantId ?? throw new InvalidOperationException(...)`)
  copiadas decenas de veces por controller en vez de centralizarse (ejemplo:
  seis copias solo en `TreasuryController.cs`).

### 5. Código limpio — "nada de lógica en los controllers"
**26 de 43 controllers en todo el backend no inyectan `IMediator`/`ISender`**
y en su lugar inyectan el DbContext del módulo directamente, con lógica de
negocio en el método del controller:
- Accounting: 10 de 16 controllers (`AccountingExportController`,
  `AeatModelsController`, `AgingController`, `FinancialStatementsController`,
  `InversionSujetoActivoController`, `IvaManagementController`,
  `ProrrataController`, `RecargoController`, `VatController`,
  `ViesController`).
- Treasury: los 5 controllers del módulo, sin excepción.
- Payroll: el único controller del módulo.
- Además: `FacturaEController` y `PublicInvoicesController` (Billing);
  `AlertsController`, `CrmNotesController`, `LeadsController` (Crm);
  `LotsController`, `SerialsController`, `ValuationController` (Inventory);
  `PurchaseOrdersController` (Purchasing); `SalesOrdersController` (Sales).

Ejemplo concreto de gravedad: `TreasuryController.cs` implementa un parser
CSV de extractos bancarios completo, con construcción de entidades EF y
`SaveChangesAsync`, directamente en la acción del controller (no en un
handler). `RecargoController.cs` calcula agrupaciones de recargo de
equivalencia y mapeo a casillas del Modelo 303 igual de inline.

Por contraste, `ClientsController.cs` (Crm) y `StockController.cs`
(Inventory) son controllers delgados: solo construyen el Command/Query y
llaman a `_mediator.Send(...)`. El patrón correcto existe en el código base,
pero no se aplicó de forma consistente — es el estándar a exigir en todo ADR
nuevo (ver checklist en `0000-template.md`).

**Validación silenciosamente muerta:** `Erp.Application/DependencyInjection.cs`
registra `ValidationBehavior` (pipeline de MediatR) pero solo escanea
validators del propio assembly `Erp.Application` (`AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`).
Ningún `Modules/*/Infrastructure/DependencyInjection.cs` registra sus propios
validators de FluentValidation — por lo que clases como
`Modules/Crm/Application/Features/Crm/Validators/CreateClientValidator.cs`
existen en el código pero **nunca se ejecutan en runtime** (`IValidator<T>`
nunca se resuelve para ellas).

### 6. Escalabilidad
- **N+1 confirmado** en `Modules/Purchasing/Application/Features/Receipts/Handlers/CreateGoodsReceiptHandler.cs`
  y `Modules/Sales/Application/Features/Deliveries/Handlers/CreateDeliveryNoteHandler.cs`:
  ambos hacen `await _context.Xxx.FindAsync(...)` dentro de un `foreach` por
  línea del documento, en vez de una única consulta batch.
- **Paginación ausente** en la mayoría de `Get*Query` de CRM, Accounting,
  Treasury, Billing e Inventory (listas sin `Skip`/`Take`). Sales sí pagina
  bien (`GetAllCustomerInvoicesQuery`, `GetDeliveryNotesQueries` devuelven un
  `Paginated...Result`) e Inventory soporta `page`/`pageSize` +
  `X-Total-Count` en `StockController.GetMovements` — es el patrón a copiar
  en el resto de módulos.
- `EcbExchangeRateProvider.cs` (Treasury) hardcodea la URL del BCE como
  `const string` en vez de `IConfiguration` — menor, pero real.
- **Añadir un módulo 10 no es autodescubrible**: exige tocar
  `backend/Erp.Api/Program.cs` manualmente en cuatro sitios (`AddApplicationPart`,
  `Add<Módulo>Infrastructure`, un bloque `AddMediatR` con un tipo de handler
  como "ancla" de assembly, y el propio `DependencyInjection.cs` del módulo).
  No hay nada que fuerce a no olvidar un paso — Payroll es la prueba: le
  falta el bloque `AddMediatR` (no tiene handlers) y nadie lo detectó hasta
  esta auditoría.

### 7. CQRS
Donde se usa, está bien aplicado: no se encontraron queries con efectos
secundarios ocultos ni commands mal nombrados como getters. Pero:
- **Payroll no tiene CQRS en absoluto.** `Modules/Payroll/Application/`
  contiene únicamente `Interfaces/IPayrollDbContext.cs` — no hay `Features`,
  `Commands`, `Queries` ni `Handlers`. `PayrollController.cs` inyecta el
  DbContext y hasta `AccountingService` (de otro módulo) directamente y
  ejecuta ahí toda la lógica de altas, liquidaciones y exportación TC1/TC2/RED.
  `Program.cs` no registra ningún `AddMediatR` para Payroll, a diferencia de
  los otros 8 módulos.
- El bypass de MediatR en 26 controllers (punto 5) es, visto desde CQRS, el
  mismo problema: esos endpoints no pasan por el pipeline de comandos/queries
  en absoluto, así que tampoco se benefician de `ValidationBehavior` ni de
  ningún pipeline behavior futuro (logging, autorización declarativa, etc.).

## Relación con otros módulos
- **ADR-0001** (arquitectura general): la violación de dirección de
  dependencias (`Erp.Infrastructure` → 5 módulos) y el patrón de "ancla de
  assembly" para `AddMediatR` son extensiones directas de lo ya descrito ahí.
- **ADR-0004** (CRM): `ClientHandlers.cs` como código muerto/ancla de
  assembly.
- **ADR-0006** (Accounting): `AccountingExportController` (SRP), duplicado
  VatController/CalculateVatCommand, `IAccountingDbContext` de 29 DbSets
  (ISP), controllers sin MediatR.
- **ADR-0008** (Inventory): entidades núcleo fuera de `Modules/Inventory/Domain`.
- **ADR-0009** (Payroll): ausencia total de CQRS.
- **ADR-0010 / ADR-0011** (Purchasing/Sales): un solo assembly, sin frontera
  de capas real; N+1 en ambos módulos.
- **ADR-0012** (Treasury): los 5 controllers sin MediatR, acoplamiento a
  Accounting.

## Buenas prácticas aplicables
Este es el checklist que queda incorporado a `0000-template.md` para todo ADR
futuro — antes de dar por cerrado un módulo o una implementación, verificar:
1. **SOLID**: ¿alguna clase concentra más de una razón de cambio (SRP)? ¿hay
   reglas de negocio que cambian con frecuencia codificadas como switch/dict
   en vez de datos (OCP)? ¿alguna interfaz obliga a implementar/depender de
   miembros que el consumidor no usa (ISP)?
2. **Clean Architecture**: ¿las referencias de proyecto solo fluyen hacia
   adentro (Api→Application→Domain, Infrastructure→Application/Domain)? ¿el
   Domain está libre de EF Core/ASP.NET? ¿el core depende de los módulos o al
   revés?
3. **Estructura de carpetas**: ¿el módulo tiene 4 `.csproj` separados
   (Api/Application/Domain/Infrastructure) con frontera real de compilador?
4. **Duplicación**: ¿existe la misma lógica de negocio implementada dos veces
   (controller vs. handler, o entre dos módulos)?
5. **Código limpio — controllers delgados**: ¿el controller solo construye
   un Command/Query y llama a `_mediator.Send(...)`, o tiene lógica de
   negocio, acceso a datos o construcción de entidades inline?
6. **Escalabilidad**: ¿los endpoints de listado paginan? ¿hay queries dentro
   de bucles (N+1)? ¿hay configuración/URLs hardcodeadas que deberían venir
   de `IConfiguration`?
7. **CQRS**: ¿el flujo pasa por `IMediator` con Command/Query separados, o el
   controller bypasea el pipeline? ¿el módulo tiene sus propios validators de
   FluentValidation registrados en su `DependencyInjection.cs` (no basta con
   que existan como clase — hay que confirmar el `AddValidatorsFromAssembly`)?

## Consecuencias
- La deuda técnica más seria por impacto es la de dirección de dependencias
  (`Erp.Infrastructure` → 5 módulos) y las entidades núcleo de Inventory
  fuera de su módulo: ambas comprometen la premisa central de "monolito
  modular" (módulos razonablemente independientes) más que cualquier
  problema de un controller aislado.
- El hallazgo de mayor volumen es el bypass de CQRS en 26/43 controllers —
  no es un caso aislado sino el patrón dominante en Accounting y Treasury.
  Cualquier trabajo futuro en esos dos módulos debería migrar el controller
  tocado a MediatR en vez de añadir más lógica al patrón existente.
- La validación de FluentValidation "muerta" es un riesgo silencioso: el
  código da la falsa sensación de estar validado (los validators existen,
  compilan, tienen tests unitarios plausibles) pero no se ejecutan en
  producción. Cualquier módulo que dependa de esta validación para
  integridad de datos está, de hecho, sin validar.
- Ninguno de estos hallazgos es bloqueante para seguir desarrollando, pero
  todos incrementan el costo de cualquier cambio futuro que toque esas zonas
  — de ahí que este ADR quede referenciado desde el template como checklist
  obligatorio, no solo como informe puntual.

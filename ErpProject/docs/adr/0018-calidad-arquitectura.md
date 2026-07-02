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

Además de la auditoría arquitectónica, este ADR mantiene dos catálogos
transversales que se actualizan a medida que se encuentran o corrigen
hallazgos: un **catálogo de datos y lógica simulada (mock)** — qué partes
del sistema aparentan funcionar pero no hacen lo que dicen — y una lista
separada de **credenciales/secretos hardcodeados**, deliberadamente fuera
del backlog de remediación por ser deuda de seguridad/configuración, no de
arquitectura.

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
- **Corregido — el mismo patrón corregido en VIES (ver ADR-0006) se repetía
  en VAT**: `backend/Modules/Accounting/Api/Controllers/VatController.cs`
  tenía su propio diccionario de tasas de IVA y cálculo inline, totalmente
  independiente de `CalculateVatCommand.cs` (que tenía otro diccionario con
  las mismas tasas más lógica real de ISP/intracomunitario/recargo y
  persistencia en `VatTransactions`). Ahora `VatController.CalculateVat`
  despacha `CalculateVatCommand` vía `IMediator`, y ambos (controller y
  handler) leen las tasas de una única fuente,
  `Application/Features/Vat/VatRates.cs` (`SpanishVatRates`). De paso se
  corrigió que `CalculateVatCommand.CompanyId` viniera del body (el cliente
  podía enviar cualquier tenant) — ahora se resuelve del `ITenantContext`
  del handler, igual que el resto de comandos de Accounting.
- **Corregido — mismo patrón, con un bug real de contrato añadido**:
  `ProrrataController.CalculateProrrata` calculaba inline con un
  `CalculateProrrataRequest` propio (`DeductibleOperations`/`NonDeductibleOperations`/`TotalVatSupported`),
  totalmente distinto del `CalculateProrrataCommand` huérfano. Al revisar el
  frontend real (`frontend/src/app/accounting/prorrata/page.tsx`) se
  encontró que este envía/lee `{fiscalYear, inlandRevenue, exemptRevenue,
  type}` / `{inlandRevenue, exemptRevenue, prorrataPercentage}` — nombres que
  no coinciden con ninguno de los dos, así que la página producía
  `TypeError: Cannot read properties of undefined (reading 'toFixed')` al
  pulsar "Calculate Prorrata". Se corrigió `CalculateProrrataCommand` para
  usar exactamente el contrato del frontend (calculadora manual: el usuario
  introduce los ingresos, se persiste un `ProrrataCalculation` real) y
  `ProrrataController` ahora despacha ese comando vía `IMediator`.
- **Corregido** — `backend/Modules/Crm/Application/Handlers/ClientHandlers.cs`
  era un set completo de queries/commands (`GetClientsModuleQuery`,
  `CreateClientModuleCommand`, etc.) que ningún controller referenciaba — el
  path real es `Application/Features/Crm/*`. La particularidad era que
  `Program.cs` usaba un tipo de ese archivo como "ancla" de assembly para
  registrar MediatR de todo el módulo CRM, así que el archivo era código
  muerto pero estructuralmente necesario tal cual estaba montado. Se eliminó
  el archivo y el ancla pasó a `Features/Crm/Handlers/GetClientsHandler`
  (handler real, usado por `ClientsController`).
- Guard clauses de `tenantId` (`_tenant.TenantId ?? throw new InvalidOperationException(...)`)
  copiadas decenas de veces por controller en vez de centralizarse (ejemplo:
  seis copias solo en `TreasuryController.cs`).

### 5. Código limpio — "nada de lógica en los controllers"
**25 de 43 controllers en todo el backend no inyectan `IMediator`/`ISender`**
(eran 26; `VatController` y `ProrrataController` se corrigieron — ver §4) y
en su lugar inyectan el DbContext del módulo directamente, con lógica de
negocio en el método del controller:
- Accounting: 8 de 16 controllers (`AccountingExportController`,
  `AeatModelsController`, `AgingController`, `FinancialStatementsController`,
  `InversionSujetoActivoController`, `IvaManagementController`,
  `RecargoController`, `ViesController`).
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

**Corregido — validación silenciosamente muerta:** `Erp.Application/DependencyInjection.cs`
registra `ValidationBehavior` (pipeline de MediatR) pero solo escaneaba
validators del propio assembly `Erp.Application` (`AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`).
Ningún `Modules/*/Infrastructure/DependencyInjection.cs` registraba sus
propios validators de FluentValidation. Revisando los 9 módulos, el único
validator de FluentValidation real (`AbstractValidator<T>`) que existía fuera
del core era `Modules/Crm/Application/Features/Crm/Validators/CreateClientValidator.cs`
— existía en el código pero **nunca se ejecutaba en runtime**
(`IValidator<CreateClientCommand>` nunca se resolvía). Se corrigió añadiendo
`AddValidatorsFromAssembly` a `AddCrmInfrastructure`. El resto de módulos no
tiene validators de FluentValidation (no hay nada más que registrar); si se
añade uno nuevo en cualquier módulo, recordar registrarlo en el
`DependencyInjection.cs` de ese módulo o seguirá sin ejecutarse.

### 6. Escalabilidad
- **Corregido** — N+1 en `Modules/Purchasing/Application/Features/Receipts/Handlers/CreateGoodsReceiptHandler.cs`
  y `Modules/Sales/Application/Features/Deliveries/Handlers/CreateDeliveryNoteHandler.cs`:
  ambos hacían `await _context.Xxx.FindAsync(...)` dentro de un `foreach` por
  línea del documento; ahora cargan todas las líneas necesarias en una única
  consulta batch (`Where(...).ToDictionaryAsync(...)`) antes del bucle.
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
- El bypass de MediatR en 25 controllers (punto 5) es, visto desde CQRS, el
  mismo problema: esos endpoints no pasan por el pipeline de comandos/queries
  en absoluto, así que tampoco se benefician de `ValidationBehavior` ni de
  ningún pipeline behavior futuro (logging, autorización declarativa, etc.).

## Relación con otros módulos
- **ADR-0001** (arquitectura general): la violación de dirección de
  dependencias (`Erp.Infrastructure` → 5 módulos) y el patrón de "ancla de
  assembly" para `AddMediatR` son extensiones directas de lo ya descrito ahí.
- **ADR-0004** (CRM): `ClientHandlers.cs` como código muerto/ancla de
  assembly (corregido).
- **ADR-0006** (Accounting): `AccountingExportController` (SRP), duplicado
  VatController/CalculateVatCommand (corregido), `IAccountingDbContext` de 29 DbSets
  (ISP), controllers sin MediatR.
- **ADR-0008** (Inventory): entidades núcleo fuera de `Modules/Inventory/Domain`.
- **ADR-0009** (Payroll): ausencia total de CQRS.
- **ADR-0010 / ADR-0011** (Purchasing/Sales): un solo assembly, sin frontera
  de capas real; N+1 en ambos módulos (corregido).
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
- La validación de FluentValidation "muerta" era un riesgo silencioso: el
  código daba la falsa sensación de estar validado (el validator existía,
  compilaba) pero no se ejecutaba en producción (corregido para CRM; ver
  ítem 6 del backlog).
- Ninguno de estos hallazgos es bloqueante para seguir desarrollando, pero
  todos incrementan el costo de cualquier cambio futuro que toque esas zonas
  — de ahí que este ADR quede referenciado desde el template como checklist
  obligatorio, no solo como informe puntual.

## Próximos pasos
Backlog de remediación, priorizado de menor a mayor riesgo/alcance. Cada
ítem se corrige de forma aislada y verificada (build/CI en verde) antes de
pasar al siguiente — nada de refactors masivos de una vez, este es software
fiscal sin tests automatizados. Estado se actualiza en este mismo ADR a
medida que se completa cada uno.

| # | Hallazgo | Módulo | Estado |
|---|---|---|---|
| 1 | Duplicado VatController/CalculateVatCommand | Accounting | ✅ Corregido |
| 2 | `ClientHandlers.cs` código muerto usado como ancla de assembly de MediatR | Crm | ✅ Corregido |
| 3a | Duplicado ProrrataController/CalculateProrrataCommand (+ bug real: contrato no coincidía con el frontend) | Accounting | ✅ Corregido |
| 3b | `RecargoController` sin `IMediator` (tiene lógica real: queries a `IBillingDbContext`, mapeo Modelo 303 — migración no trivial) | Accounting | Pendiente |
| 3c | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `FinancialStatementsController`, `AgingController` sin `IMediator` | Accounting | Deprioritizado — ver nota |
| 4 | `AccountingExportController` (SRP, 1362 líneas, 6 módulos inyectados) | Accounting | Pendiente |
| 5 | `ViesController` (Accounting) sigue duplicando lo que ya resuelve `Erp.Api/TaxController` | Accounting | Pendiente |
| 6 | Validators de FluentValidation nunca registrados por módulo (`AddValidatorsFromAssembly` ausente) | Crm | ✅ Corregido |
| 7 | N+1 en `CreateGoodsReceiptHandler` / `CreateDeliveryNoteHandler` | Purchasing / Sales | ✅ Corregido |
| 8 | Paginación ausente en `Get*Query` (CRM, Accounting, Treasury, Billing, Inventory) | Varios | Pendiente |
| 9 | Los 5 controllers de Treasury sin `IMediator` (incluye parser CSV inline) | Treasury | Pendiente |
| 10 | Payroll sin capa CQRS/MediatR | Payroll | Pendiente |
| 11 | `PurchaseOrdersController` / `SalesOrdersController` sin `IMediator` | Purchasing / Sales | Pendiente |
| 12 | Sales y Purchasing: un solo assembly, sin frontera de capas real | Purchasing / Sales | Pendiente |
| 13 | `Erp.Infrastructure` depende de 5 módulos (dirección invertida) | Core | Pendiente |
| 14 | Entidades núcleo de Inventory fuera de `Modules/Inventory/Domain` | Inventory | Pendiente |
| 15 | `ConsolidationController.ConsolidateGroup` responde éxito sin consolidar nada; `ConsolidatedFinancialStatements` nunca se escribe | Treasury | Pendiente |
| 16 | `ExchangeRateRefreshJob` no hace nada nunca: corre fuera de contexto HTTP y `TenantContext.TenantId` siempre es `null` ahí, sin log ni error | Treasury | Pendiente |
| 17 | `frontend/billing/facturae/page.tsx` — página 100% mock, botones sin `onClick`, mientras el backend (`FacturaEController`) sí es real | Billing (frontend) | Pendiente |
| 18 | `frontend/accounting/iva-registers/page.tsx` — página 100% mock con cifras inconsistentes entre cabecera y tabla | Accounting (frontend) | Pendiente |
| 19 | `OutboxMessageProcessorJob.cs` — implementación completa y correcta del procesador de Outbox, pero huérfana: nunca se registra, existe un duplicado distinto que sí corre (`OutboxProcessorJob.cs`) | Core | Pendiente |

Los ítems 13 y 14 son los de mayor riesgo/alcance (tocan la dirección de
dependencias del monolito modular entero) y deberían abordarse solo después
de validar los anteriores, con más contexto y posiblemente en su propia
rama/PR dedicado — no como parte de este barrido incremental.

**Nota sobre el ítem 3c (deprioritizado):** `AeatModelsController`,
`IvaManagementController`, `InversionSujetoActivoController`,
`FinancialStatementsController` y `AgingController` no tienen ninguna lógica
real detrás (ni persistencia, ni cálculo a partir de datos reales, solo
`return Ok(new { ...cifras fijas... })`). Envolver eso en un
Command/Query/Handler de MediatR sería ceremonia sin beneficio real: no
arregla el problema de fondo (que son informes fiscales inventados) y añade
indirección a código que no tiene ningún comportamiento que proteger de
duplicación. A diferencia de VAT y Prorrata, aquí no hay una segunda
implementación "real" con la que unificar. Migrarlos a CQRS solo tiene
sentido cuando se implemente la lógica fiscal real correspondiente — hacerlo
antes sería una abstracción prematura. Se dejan documentados como mock en
ADR-0006 y fuera de este barrido de "controllers delgados".

## Catálogo de datos y lógica simulada (mock)

Barrido dedicado (dos agentes de investigación en paralelo, sin reutilizar
solo lo ya sabido) para responder una pregunta concreta: **¿qué partes del
sistema aparentan funcionar pero no hacen lo que dicen hacer?** Cada fila
está verificada leyendo el código citado — no es una sospecha, es un hecho
confirmado. Los ítems marcados "→ backlog #N" ya están en la tabla de
Próximos pasos; el resto son hallazgos nuevos de este barrido, añadidos como
ítems 15-19 arriba.

| Módulo | Qué aparenta hacer | Qué hace en realidad | Ref. |
|---|---|---|---|
| Accounting | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `FinancialStatementsController`, `AgingController` calculan/declaran modelos fiscales reales | Devuelven cifras fijas hardcodeadas, sin persistencia ni cálculo real | ADR-0006, backlog #3c |
| Accounting | `ViesController` valida NIF-IVA contra el registro VIES de la UE | Diccionario estático de 4 NIFs de prueba; el válido de verdad está en `Erp.Api/TaxController` | ADR-0006/0013, backlog #5 |
| Accounting (frontend) | `iva-registers/page.tsx` muestra libros de IVA reales exportables a SII | Página 100% estática; cifras de cabecera y de la tabla ni siquiera coinciden entre sí; botones "Descargar .TXT"/"Enviar a SII" sin `onClick` | **Nuevo → backlog #18** |
| Billing (frontend) | `billing/facturae/page.tsx` gestiona documentos FacturaE reales (firmar, enviar a VERI\*FACTU) | Página 100% estática con array hardcodeado; los 5 botones no tienen `onClick`. El backend (`FacturaEController`) sí es real — es solo el frontend el que está desconectado | **Nuevo → backlog #17** |
| Treasury | `ConsolidationController.ConsolidateGroup` consolida estados financieros de un grupo empresarial | Solo comprueba que el grupo existe y responde `"Consolidated"`; no agrega nada. `ConsolidatedFinancialStatements` (la tabla que lee `GetFinancialStatements`) nunca se escribe en ningún sitio del código — es una tabla de solo lectura que siempre estará vacía | **Nuevo → backlog #15** |
| Treasury | `ExchangeRateRefreshJob` actualiza tipos de cambio a diario desde el BCE (`EcbExchangeRateProvider`, que sí está bien implementado) | El job corre como `BackgroundService` fuera de contexto HTTP, así que `TenantContext.TenantId` siempre es `null` ahí (solo lo puebla `TenantResolverMiddleware` por request) — el job hace no-op silencioso todos los días, para siempre, sin log ni error visible | **Nuevo → backlog #16** |
| Automatización | Motor de reglas evalúa condiciones y ejecuta acciones automáticas | `CreateRuleCommand` nunca persiste, el frontend (`settings/automation`) es JSX estático sin `fetch`, y el único job real (`RuleEvaluatorJob`) nunca se registra en Hangfire | ADR-0015, backlog (ver ADR-0015) |
| API pública | Sistema unificado de API Keys con rate limiting | Dos sistemas paralelos y desconectados; `PublicApiController` depende de un validator no registrado en DI | ADR-0016 |
| Audit Logs | Interceptor de `SaveChangesAsync` audita todos los cambios automáticamente | El interceptor existe pero nunca se invoca desde ningún `SaveChangesAsync` — `AuditLogsController` consulta una tabla que en la práctica no se puebla | ADR-0017 |
| Suscripciones | `Subscription.ActiveModules` (JSONB) determina qué módulos tiene activos un tenant | Se escribe al dar de alta, pero el gating real (`ModuleAuthorizationHandler`) usa exclusivamente `TenantModules`/`Plan.PlanModules` — ese JSONB es dato muerto | ADR-0014 |
| Core (Outbox) | `OutboxMessageProcessorJob.cs` procesa el outbox transaccional | Implementación completa y correcta, pero **huérfana**: nunca se registra en Hangfire. El que realmente corre es un archivo distinto, `OutboxProcessorJob.cs` (registrado en `Program.cs:322`, con `SELECT ... FOR UPDATE SKIP LOCKED` para escalado horizontal) — dos implementaciones del mismo concepto, una muerta | **Nuevo → backlog #19** |

**Módulos confirmados sin datos simulados** (verificado explícitamente, no
solo "no se encontró nada"): CRM (Clients/Contacts/Leads/Suppliers/Alerts/Notes
son CRUD real), Payroll (real, y sus exports TC1/TC2/RED se auto-etiquetan
honestamente como "documento orientativo" vía `FiscalExportHeaders.MarkAsNonOfficial`
— no es un mock oculto, es un disclaimer explícito), Expenses (OCR con
Tesseract real), Inventory, Sales y Purchasing (CRUD real en sus
controllers), y el resto de Treasury (`GuaranteesController`,
`FinancingController`, `CurrenciesController`, `TreasuryController`).

## Credenciales y secretos hardcodeados

> **Esto es lo último de lo último a corregir.** No forma parte de la tabla
> de Próximos pasos: es deuda de seguridad/configuración, no de arquitectura,
> y su prioridad de negocio la decide el usuario, no este ADR. Se documenta
> aquí únicamente para que quede registrado y nadie asuma que no existe.

Barrido dedicado sobre todo el repo (backend, frontend, docker-compose,
`deploy/`, CI/CD, `k8s/`). El patrón general del proyecto es correcto —
CI/CD usa `${{ secrets.* }}`, Kubernetes usa `secretKeyRef`, `.env.example`
solo tiene nombres de variable vacíos con instrucciones para generarlas, y
no hay ningún `.env` real commiteado. Los hallazgos reales están todos
concentrados en `appsettings.Development.json` y dos *fallbacks* de código:

| Severidad | Archivo | Qué hay |
|---|---|---|
| **Alta** | `backend/Erp.Api/appsettings.Development.json:14` | Connection string de Postgres apuntando a una IP externa real (`89.167.102.120:5433`), usuario `postgres`, contraseña `123456` |
| **Alta** | `backend/Erp.Api/appsettings.Development.json:18` | Contraseña del admin semilla, `DevChangeMe2026!!` (usada por `Program.cs` para crear `admin@devcorp.com` vía BCrypt si la BD está vacía y `ASPNETCORE_ENVIRONMENT=Development`) |
| Media | `backend/Erp.Api/Controllers/AdminController.cs:28` | El acceso de super-admin está gateado a un email literal (`admin@devcorp.com`) en vez de un rol/claim — no se puede rotar sin redeploy, y combinado con la fila anterior es una credencial completa conocida |
| Media | `backend/Erp.Api/Program.cs:405-414` | Mismo email `admin@devcorp.com` hardcodeado en la lógica de seed |
| Media | `backend/Erp.Infrastructure/Messaging/RabbitMqConnectionFactory.cs:37` | Fallback silencioso a las credenciales por defecto de RabbitMQ (`guest:guest`) si no se configura `RabbitMQ:Uri` — mitigado porque RabbitMQ está deshabilitado por defecto |
| Media | `backend/Erp.Infrastructure/Services/Storage/MinioFileStorageService.cs:32-33` | Fallback silencioso a las credenciales por defecto de MinIO (`minioadmin`/`minioadmin`) si no se configuran `Storage:AccessKey`/`Storage:SecretKey` |

El resto de valores encontrados (Stripe `sk_test_dummy`, SMTP `dummy`/`dummy`,
JWT secret de `appsettings.Development.json` autoetiquetado "change-in-prod-via-env",
connection string `localhost`/`postgres`/`postgres` de dev) son placeholders
obviamente falsos o correctamente vacíos en el template de producción — no
se listan como hallazgo porque no representan una credencial real filtrada.

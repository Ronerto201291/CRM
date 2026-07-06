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

**Hallazgo nuevo — Inventory (✅ corregido ADR-0018 #14):** `Product`, `Warehouse`, `Stock` y
`StockMovement` viven en `Modules/Inventory/Domain/Entities/` (antes físicamente en
`Erp.Domain/Modules/Inventory/` con el namespace correcto pero fuera del módulo).

### 3. Estructura de carpetas
7 de 9 módulos siguen el patrón `Api/Application/Domain/Infrastructure` con
cuatro `.csproj` separados (frontera real de compilador entre capas).
Desviaciones:
- **Inventory**: la carpeta es `Modules/Inventory/Api` (✅ corregido jul 2026, ADR-0018 #19f); antes era `API` con casing incorrecto que rompía builds Linux/Docker.
- **Sales y Purchasing**: ~~un único `.csproj`~~ **✅ corregido (#12):** cuatro `.csproj` por módulo
  (`Api/Application/Domain/Infrastructure`) con frontera real de compilador, igual que los otros 7.
  Purchasing: migraciones bajo `Infrastructure/Migrations/` (#19g).

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
**0 controllers en todo el backend quedan sin `IMediator`/`ISender`**
(eran 26 de 43 en el momento de esta auditoría original; Treasury #9,
AccountingExport #4, Inventory completo, Billing
`FacturaEController`/`PublicInvoicesController`, Payroll #10, core legacy
`TaxController`/`SiiController`/`SubscriptionController`/`FiscalCalendarController`, etc.) y
en su lugar inyectaban el DbContext del módulo directamente, con lógica de
negocio en el método del controller. **Recuento actualizado (contra-auditoría
jul 2026):** el backend ha crecido desde entonces (`PosTerminalsController`,
`AccountantExportController` y otros nuevos) hasta 71 controllers en total —
verificado de nuevo con grep de `DbContext` sobre los 71 archivos: 0
coincidencias. Las únicas 2 excepciones sin `IMediator` puro
(`FiscalHomologationController` con `IConfiguration`, `StripeWebhookController`
con `StripeService`) no tienen lógica de negocio ni acceso a datos inline y
están exentas a propósito en `Erp.ArchitectureTests`.
- Accounting: los 5 stubs mock (`AeatModelsController`, `AgingController`,
  `InversionSujetoActivoController`, `IvaManagementController`) fueron
  **reescritos con MediatR real** (#3c) — corrección de wording (contra-auditoría
  jul 2026): los propios archivos de controller siguen existiendo en el repo
  y siguen recibiendo cambios (ver el commit de permisos jul 2026 citado en
  el cierre de backlog); lo que se eliminó fue la lógica mock que tenían
  dentro, no los controllers. `FinancialStatementsController` usa MediatR
  con handlers honestos `NotImplemented`. `AccountingExportController` ✅
  (#4); `RecargoController` en #3b; `ViesController` en #5.
- Treasury: ninguno pendiente (5/5 vía `IMediator`; ver backlog #9).
- Payroll: `PayrollController` ✅ delgado — solo `IMediator` (#10).
- Billing: todos los controllers del módulo usan `IMediator` (FacturaE vía
  `GenerateFacturaEQuery`; API pública vía `GetPublicInvoicesQuery`).
- Inventory: todos los controllers del módulo usan `IMediator`.

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
secundarios ocultos ni commands mal nombrados como getters.

**Corregido (contradicción interna detectada en contra-auditoría jul 2026):**
esta sección seguía afirmando "Payroll no tiene CQRS en absoluto" pese a que
el backlog #10 (más abajo) y la sección 5 ya documentaban esa corrección como
cerrada. Confirmado de nuevo contra el código real: `Modules/Payroll/Application/Features/{Employees,Settlements,Exports}/`
existe con Commands/Queries/Handlers reales; `PayrollController.cs` solo
inyecta `IMediator`; `Program.cs` registra `AddMediatR` para el assembly de
Payroll. El texto que decía lo contrario en esta sección estaba obsoleto, no
el código — se elimina para no volver a contradecir el propio backlog.

El bypass de MediatR en los controllers que aún no usaban `IMediator` (punto
5, ya cerrado) era, visto desde CQRS, el mismo problema: esos endpoints no
pasaban por el pipeline de comandos/queries en absoluto, así que tampoco se
beneficiaban de `ValidationBehavior` ni de ningún pipeline behavior futuro
(logging, autorización declarativa, etc.).

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
- **ADR-0009** (Payroll): CQRS/MediatR en `Features/{Employees,Settlements,Exports}/`
  (backlog #10 ✅; antes ausencia total de CQRS).
- **ADR-0010 / ADR-0011** (Purchasing/Sales): cuatro `.csproj` por módulo (#12 ✅);
  N+1 en recepciones/albaranes corregido (#7).
- **ADR-0012** (Treasury): controllers vía `IMediator` (#9 ✅); acoplamiento a
  Accounting resuelto con puertos en `Erp.Application` (#19c).

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
- El hallazgo de mayor volumen histórico fue el bypass de CQRS en controllers
  (✅ cerrado: todos los controllers de módulo vía `IMediator`; los stubs mock
  de Accounting fueron reescritos con MediatR real en #3c — sus archivos de
  controller no se eliminaron, solo la lógica mock que tenían). Cualquier
  trabajo futuro en Accounting debe implementar lógica fiscal real en
  handlers, no reintroducir datos fijos.
- La validación de FluentValidation "muerta" era un riesgo silencioso: el
  código daba la falsa sensación de estar validado (el validator existía,
  compilaba) pero no se ejecutaba en producción (corregido para CRM; ver
  ítem 6 del backlog).
- Ninguno de estos hallazgos es bloqueante para seguir desarrollando, pero
  todos incrementan el costo de cualquier cambio futuro que toque esas zonas
  — de ahí que este ADR quede referenciado desde el template como checklist
  obligatorio, no solo como informe puntual.

## Hallazgos críticos — código real pero incorrecto frente a especificación externa

Categoría distinta del catálogo de mock de más abajo: esto **no es código
que finge funcionar** (no hay ningún `Ok(new {...})` hardcodeado) — es
código real que llama de verdad a la AEAT/Stripe/un banco, firma
digitalmente, calcula hashes. La auditoría de esta sección responde a una
pregunta que ninguna auditoría anterior de este ADR se había hecho: **el
código que SÍ es real, ¿es además correcto frente a la especificación
externa exacta que dice implementar?** Cada fila está verificada leyendo el
archivo citado, no es una sospecha. Detalle completo por módulo en
ADR-0013 (VeriFactu/SII/FacturaE), ADR-0012 (SEPA) y ADR-0014 (Stripe).

| # | Hallazgo | Módulo | Prioridad |
|---|---|---|---|
| 0a | **VeriFactu** — ~~huella/XML/anulaciones/auditoría/leyenda~~ ✅ **Cerrado** — modo no-VERI*FACTU: `Verifactu:SubmissionMode=LocalOnly` (sin envío TIKE, sin QR, leyenda PDF alternativa, `VerifactuRealtimeSubmission` en factura, `TipoUsoPosibleSoloVerifactu=N`) | Billing/Core | ✅ Corregido |
| 0b | **SII** — **🟡 Parcial+** — namespaces duales + XAdES-BES; homologación offline `GET /api/sii/validate` + `SiiXmlStructureValidator` (Titular/NIF, `PeriodoLiquidacion`, registros); envío HTTP real con `Sii:SendEnabled=true` (desactivado por defecto). `GET /api/fiscal/homologation/status` documenta bloqueo externo. Pendiente: homologación AEAT en entorno test | Core | **Crítica — bloqueado externo** |
| 0c | **FacturaE** — **🟡 Parcial+** — firma **XAdES-EPES** en `FacturaESigningService` (`SignaturePolicyIdentifier` + política Facturae v3.1); `FacturaEXmlStructureValidator` rechaza firmas sin política; namespace 3.2.2 unificado. Pendiente: homologación FACe entorno test (certificado + envío real) | Billing | Alta — bloqueado externo homologación |
| 0d | **SEPA** — **🟡 Parcial+** — endpoints pain.001/pain.008 + `SepaXmlStructureValidator` offline (`GrpHdr`, `PmtInf`, `CdtTrfTxInf`/`DrctDbtTxInf`). `GET /api/fiscal/homologation/status` marca `bankHomologation=false`. Pendiente: homologación bancaria | Treasury | Alta — bloqueado externo |
| 0e | **Stripe — idempotencia webhook + claves test/live** | Core (StripeService) | ✅ Corregido — `StripeWebhookEvents` + skip por `EventId`; `StripeOptionsValidator` (`IValidateOptions`) exige `sk_test_` fuera de Production y `sk_live_` en Production (+ prefijo `pk_*` si `PublishableKey` está definido); 3 tests unitarios |
| 0f | **Sin validación de NIF/CIF/NIE** | CRM/Billing/Core | ~~el CIF tenía la lógica de letra/dígito de control invertida (A/B/E/H recibían letra en vez de dígito, P/Q/S recibían dígito en vez de letra); el CIF real de Banco Santander `A39000013` era rechazado~~ **✅ Corregido** — ramas de `ValidateCif` corregidas (A/B/E/H → dígito; P/Q/S → letra; resto acepta cualquiera de los dos, que es la regla real para esos prefijos). Verificado de nuevo con cálculo manual: `A39000013` y `A28015865` (Telefónica) ya se aceptan. El test `FindValidCif` (que generaba su propio caso por fuerza bruta contra el validador roto, dando falsa confianza) se sustituyó por casos con CIFs reales conocidos + casos de rechazo con dígito de control alterado. Se eliminó también `tmp-find-cif.cs`, un resto de depuración commiteado por error. NIF/NIE seguían y siguen bien |
| 0g | **Ninguna empresa registrada por `/register`/`/signup` tenía plan contable — cualquier cobro de factura fallaba** | Core (Auth) ↔ Accounting | ~~`PgcSeeder.cs` (`Modules/Accounting/Infrastructure/Seeding/`) sembraba el PGC pero no lo invocaba nadie en todo el repo (verificado: 0 referencias); el único seeding real vivía inline en `Program.cs`, dentro de un bloque `if (!dbContext.Users.Any())` — solo se ejecuta la primera vez que la BBDD está vacía, para la empresa de desarrollo. Cualquier empresa dada de alta después (flujo normal de negocio) quedaba sin ninguna cuenta, y `PaymentReceivedEventHandler` lanza `InvalidOperationException` al buscar "570"/"572"/"430" por código exacto — el cobro de cualquier factura de cualquier empresa real fallaba. Además `PgcSeeder.cs` tenía bugs propios: códigos de cuenta duplicados (`"210"` y `"110"` cada uno tres/dos veces), `Type` en español (no usado por ningún filtro) y encoding roto (mojibake)~~ **✅ Corregido** — nuevo `CompanyCreatedEvent` (publicado por `RegisterCompanyHandler` y `AddCompanyFromAccountHandler` tras crear la empresa) consumido por `SeedChartOfAccountsHandler` (`Modules/Accounting/Application/Handlers/`, reemplaza y borra el `PgcSeeder.cs` muerto), con la lista de cuentas corregida (Types en inglés consistentes con `GetBalanceSheetQuery`/`GetProfitAndLossQuery`/`GetMayorQuery`, sin códigos duplicados). El bloque inline de `Program.cs` también se sustituyó por el mismo evento — una única fuente de verdad, idempotente. 7 tests nuevos (`SeedChartOfAccountsHandlerTests`, más aserciones en `RegisterCompanyHandlerTests`/`AddCompanyFromAccountHandlerTests` verificando que el evento se publica) |

**Por qué esto es más grave que el resto del backlog de código**: los ítems
1-19 son deuda técnica que hace el código más difícil de mantener, pero
funciona. Los ítems 0a-0f son al revés — el código funciona (no lanza
excepciones, no requiere arreglar nada para "que compile"), pero el
resultado que produce sería rechazado por un tercero externo (AEAT, un
banco) si se usara en producción real. Es el tipo de deuda más peligroso
porque no se detecta con ningún build verde ni con pruebas manuales
superficiales — solo se descubre al intentar el envío real u homologar el
software, momento en el que ya hay facturas/pagos reales de por medio.

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
| 3b | `RecargoController` sin `IMediator` (tiene lógica real: queries a `IBillingDbContext`, mapeo Modelo 303 — migración no trivial) | Accounting | ✅ Corregido |
| 3c | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `AgingController` sin `IMediator` | Accounting | ✅ Corregido — reescritos con MediatR real jul 2026 (los controllers siguen existiendo, se reemplazó la lógica mock, no se borraron los archivos); modelos 303/347 reales; `FinancialStatementsController` vía MediatR (#26); `GET /api/accounting/aging` real (jul 2026) |
| 4 | `AccountingExportController` (SRP, ~350 líneas tras extracción) — el motivo estructural del acoplamiento cross-módulo está en el ítem 19b | Accounting | ✅ Corregido — 16/16 rutas vía `IMediator`; controller delgado; `modelo347-aeat-txt` en `ExportModelo347AeatTxtQuery` + `IModelo347Exporter.ExportAeatTxtAsync` |
| 5 | `ViesController` (Accounting) sigue duplicando lo que ya resuelve `Erp.Api/TaxController` | Accounting | ✅ Corregido |
| 6 | Validators de FluentValidation nunca registrados por módulo (`AddValidatorsFromAssembly` ausente) | Crm | ✅ Corregido |
| 7 | N+1 en `CreateGoodsReceiptHandler` / `CreateDeliveryNoteHandler` | Purchasing / Sales | ✅ Corregido |
| 8 | Paginación ausente en `Get*Query` (CRM, Accounting, Treasury, Billing, Inventory) | Varios | ✅ Corregido — CRM, Billing, Inventory, Accounting (`GetJournalQuery`) y Treasury (`GetBankMovementsQuery`, `GetCashEffectsQuery`, `GetPaymentOrdersQuery`) paginados con `X-Total-Count` |
| 9 | Los 5 controllers de Treasury sin `IMediator` (incluye parser CSV inline) | Treasury | ✅ Corregido — los 5 controllers del módulo delegan en handlers (`TreasureHandlers`, `GuaranteeHandlers`, `FinancingHandlers`, `ConsolidationHandlers`) |
| 10 | Payroll sin capa CQRS/MediatR | Payroll | ✅ Corregido — `PayrollController` solo `IMediator`; `Features/{Employees,Settlements,Exports}/`; exportes TC1/TC2/RED en `PayrollExportHandlers.cs` |
| 11 | `PurchaseOrdersController` / `SalesOrdersController` sin `IMediator` | Purchasing / Sales | ✅ Corregido — handlers en `Application/Features/Orders/`; controllers delgados; filtro por tenant en queries |
| 12 | Sales y Purchasing: frontera de capas por compilador | Purchasing / Sales | ✅ — 4 proyectos por módulo; **todos los módulos** en `Erp.slnx` (Accounting, Expenses, Treasury, Payroll añadidos jul 2026) |
| 13 | `Erp.Infrastructure` depende de módulos Application (dirección invertida) | Core | ✅ Corregido jul 2026, **reabierto y vuelto a corregir jul 2026** — primer arreglo: `IVerifactuXmlGenerator` movido a `Erp.Application/Common/Interfaces/`, implementación en `Billing.Infrastructure`, eliminada `ProjectReference` `Erp.Infrastructure`→`Billing.Application`. **Regresión real**: el mismo commit que cerró este ítem añadió `Erp.Infrastructure/Services/GestoriaDashboardDataQuery.cs` inyectando directamente `IBillingDbContext`/`IExpensesDbContext` y añadiendo `ProjectReference` nuevas a `Billing.Application`/`Expenses.Application`/`Purchasing.Application` — la propia auditoría que declaraba el ítem cerrado no se ejecutó contra el diff completo del commit. Corregido de nuevo: `GestoriaDashboardDataQuery` ahora solo depende de los puertos `IAutomationBillingQuery.GetInvoicesIssuedSinceAsync`/`IAutomationExpensesQuery.GetApprovedExpensesSinceAsync` (implementados en `Billing.Infrastructure`/`Expenses.Infrastructure`); eliminadas las 3 `ProjectReference` de `Erp.Infrastructure.csproj`. La regla queda reforzada con un test de dos capas en `Erp.ArchitectureTests/DependencyDirectionArchitectureTests.cs` — `CoreCsproj_DoesNotReferenceModuleApplicationProjects` (parsea el `.csproj` como texto, no depende de que el ensamblado cargue) y `CoreAssemblies_DoNotReferenceModuleApplicationAssemblies` (verificación por reflexión) — que sustituye a la `ErpInfrastructure_DoesNotReferenceModuleApplicationLayers` original (insuficiente: solo miraba un caso conocido, no cualquier referencia nueva a `*.Application` de un módulo). Ver también CLAUDE.md, sección "Antes de dar por terminado un cambio", que documenta este incidente como caso de no-regresión obligatorio |
| 14 | Entidades núcleo fuera de su módulo (Inventory + Accounting 6 entidades en `Erp.Domain`) | Inventory, Accounting | ✅ Corregido — todas las entidades Accounting migradas a `Accounting.Domain`; Inventory (`Product`, `Warehouse`, `Stock`, `StockMovement`) físicamente en `Modules/Inventory/Domain/Entities` |
| 15 | `ConsolidationController.ConsolidateGroup` responde éxito sin consolidar nada; `ConsolidatedFinancialStatements` nunca se escribe | Treasury | ✅ Corregido — `ConsolidateGroupHandler` agrega P&amp;L y balance desde `JournalEntryLine` (PGC 6/7/1-5) por filial + matriz, ponderado por `OwnershipPercentage`; persiste en `ConsolidatedFinancialStatements` |
| 16 | `ExchangeRateRefreshJob` no hace nada nunca: corre fuera de contexto HTTP y `TenantContext.TenantId` siempre es `null` ahí, sin log ni error | Treasury | ✅ Corregido — `RefreshAllTenantsRatesAsync` itera empresas con `IgnoreQueryFilters`; el job llama a ese método; log cuando no hay divisas activas |
| 17 | `frontend/billing/facturae/page.tsx` — página 100% mock, botones sin `onClick`, mientras el backend (`FacturaEController`) sí es real | Billing (frontend) | ✅ Corregido |
| 18 | `frontend/accounting/iva-registers/page.tsx` — página 100% mock con cifras inconsistentes entre cabecera y tabla | Accounting (frontend) | ✅ Corregido — resumen desde `/api/proxy/invoices` y `/api/proxy/expenses/documents`; descarga CSV real vía `libro-iva-emitidas`/`libro-iva-recibidas`; enlace a `/sii` para envío |
| 19 | `OutboxMessageProcessorJob.cs` — implementación completa y correcta del procesador de Outbox, pero huérfana: nunca se registra, existe un duplicado distinto que sí corre (`OutboxProcessorJob.cs`) | Core | ✅ Corregido — eliminado `OutboxMessageProcessorJob.cs`; `OutboxProcessorJob` (registrado en `Program.cs`) es la única implementación activa |
| 19a | **Dependencia circular real entre dos módulos**: ~~`Modules/Crm/Api/Erp.Modules.Crm.Api.csproj:15` referencia `Modules/Expenses/Application`, y a la vez `Modules/Expenses/Api/Erp.Modules.Expenses.Api.csproj:15` referencia `Modules/Crm/Application`~~ **✅ Corregido** — eliminada `ProjectReference` Crm.Api→Expenses.Application; gastos por proveedor vía `GET /api/expenses/by-supplier/{id}`; ficha proveedor (`suppliers/page.tsx`) hace fetch paralelo. `Expenses.Api` ya no referencia `Crm.Application` (véase #19d) | Crm ↔ Expenses | ✅ Corregido |
| 19b | `Accounting.Api` referencias cruzadas a otros módulos | Accounting | ✅ Corregido — sin refs a Billing/Crm/Expenses/Payroll ni `Erp.Infrastructure`; lectura cross-módulo solo en `Accounting.Infrastructure` (exporters) |
| 19c | Acoplamiento Treasury/Payroll → Accounting sin abstracción | Treasury, Payroll, Accounting | ✅ Corregido — `IBankReconciliationLedgerQuery`, `IPayrollJournalEntryGenerator`, `IConsolidationMetricsQuery` en `Erp.Application`; `Treasury.Application` sin ref a `Accounting.Application`; `ConsolidateGroupHandler` usa puerto de métricas |
| 19d | `ExpensesController` inyecta `ICrmDbContext` y escribe directamente en `_crmCtx.ActivityLogs` (`Modules/Expenses/Api/Controllers/ExpensesController.cs:3,23,31,72`) — un módulo de negocio escribiendo en la tabla de otro sin pasar por su API/eventos. Contraste positivo ya existente en el propio código: los `EventHandlers` de Inventory y Crm (`ExpenseApprovedInventoryHandler`, `QuoteAcceptedCrmHandler`, etc.) sí reaccionan a eventos de MediatR sin inyectar el DbContext ajeno — ese es el patrón a copiar aquí | Expenses, Crm | ✅ Corregido — upload QR publica `ExpenseUploadCreatedEvent`; `ExpenseUploadActivityHandler` (CRM) escribe `ActivityLog`; eliminada `ProjectReference` Crm desde `Expenses.Api` |
| 19e | Self-registro modular `IErpModule` + `ErpModuleExtensions` | Todos (Core) | ✅ Corregido — los **9 módulos** (Accounting, Billing, CRM, Treasury, Purchasing, Inventory, Expenses, Payroll, Sales) vía `*ErpModule`; `Program.cs` solo registra `AddErpModule` + `AddErpModuleControllers` por módulo |
| 19f | Inventory usa `API` en vez de `Api` no solo como nombre de carpeta sino en el namespace real compilado: `namespace Erp.Modules.Inventory.API.Controllers` en sus 5 controllers, referenciado explícitamente en `Program.cs:56` (`typeof(Erp.Modules.Inventory.API.Controllers.ProductsController)`) — inconsistente con los otros 8 módulos (`Erp.Modules.<Nombre>.Api.Controllers`) | Inventory | ✅ Corregido jul 2026 — namespace unificado `Erp.Modules.Inventory.Api` |
| 19g | Cosmético, agrupado: carpeta de migraciones de Purchasing en `Modules/Purchasing/Migrations/` en vez de `Modules/Purchasing/Infrastructure/Migrations/` (como los otros 8 módulos); `Modules/Sales/Infrastructure/DependencyInjection.cs` usa namespace de bloque (`namespace X { }`) en vez de namespace de archivo (`namespace X;`) como el resto del código | Purchasing, Sales | ✅ Corregido jul 2026 — Purchasing migrations + Sales DI file-scoped |

**Verificaciones limpias de esta auditoría de modularidad (sin hallazgos, se deja constancia explícita para no repetir el análisis):** ningún módulo con proyectos separados (los 7 que no son Sales/Purchasing) tiene su Domain o Application referenciando su propia Infrastructure; no se encontró ninguna otra entidad de negocio (Billing, Crm, Expenses, Payroll, Purchasing, Sales, Treasury) mal ubicada en `Erp.Domain` (ítem 14 ✅); el frontend (`frontend/src/app/`) está limpiamente separado por módulo en carpetas, sin ningún import cruzado entre carpetas de módulos distintos.

**Ítems 20+: mejoras funcionales (nivel producto).** A diferencia de 1-19
(deuda de código: duplicación, CQRS, estructura), estos son huecos
funcionales — el ERP hace CRUD correcto por módulo pero no los conecta como
procesos de negocio reales, o tiene funcionalidad fiscal/financiera
simulada en vez de real. Alcance y esfuerzo mayores; no son "arreglar algo
roto" sino "construir la integración/lógica que falta". Priorizados por
relación esfuerzo/impacto (los primeros reutilizan código que ya existe).

| # | Mejora | Módulos | Prioridad |
|---|---|---|---|
| 20 | `GoodsReceipt` (Purchasing) no incrementa stock en Inventory al recibir mercancía — sin esto, "recepción de compra" no tiene efecto real en el almacén | Purchasing → Inventory | ✅ Corregido — `CreateGoodsReceiptHandler` publica `GoodsReceiptCreatedEvent`; `GoodsReceiptInventoryHandler` incrementa stock |
| 21 | `DeliveryNote` (Sales) no decrementa stock en Inventory al entregar — sin esto, "entrega de pedido" no descuenta existencias | Sales → Inventory | ✅ Corregido — `CreateDeliveryNoteHandler` publica `DeliveryNoteCreatedEvent`; `DeliveryNoteInventoryHandler` decrementa stock |
| 22 | `Sales.CustomerInvoice` y `Billing.Invoice` | Sales ↔ Billing | ✅ Corregido — líneas operativas sin fiscal; totales header (`SubTotal`/`TaxAmount`/`Total`) documentados como cache desnormalizado desde Billing al crear |
| 23 | `SalesOrder.ClientId`/`ClientName` es texto suelto sin FK real a `Crm.Client` — Sales no está realmente integrado con CRM | Sales ↔ Crm | ✅ Corregido — `CreateSalesOrderHandler` valida `ClientId` vía `IClientInfoService` y fija `ClientName` desde CRM; migración `FK_SalesOrders_Clients_ClientId`; frontend `sales/orders/new` envía `clientId` |
| 24 | Implementar Modelo 303 (IVA trimestral) real a partir de facturas bloqueadas + `JournalEntry` (472 deducible) | Accounting | ✅ Corregido — `Modelo303Reader`, `GET /api/accounting/modelo-303`, export CSV/XML; frontend `accounting/aeat` conectado |
| 25 | Implementar Modelo 347 (operaciones anuales >3.005,06€) real, sustituyendo el stub | Accounting | ✅ Corregido — `Modelo347Reader` + `GetModelo347JsonQuery`; export CSV/TXT; frontend `accounting/aeat` con consulta preview y descargas |
| 26 | Estados financieros reales desde `JournalEntry`/`JournalEntryLine` | Accounting | ✅ Corregido — PyG/balance vía `ReportsController` (`GetProfitAndLossQuery`, `GetBalanceSheetQuery`); EFE/patrimonio en `FinancialStatementsController`; frontend `accounting/reports` con errores visibles |
| 27 | Activar el motor de automatización: registrar `RuleEvaluatorJob` en Hangfire, hacer que `CreateRuleCommand` persista, conectar `settings/automation` al backend — la lógica de negocio (facturas vencidas, stock bajo) ya existe, solo falta cablearla | Core/Automatización | ✅ Corregido — job diario 9:00; API + frontend; `DatabaseRuleEvaluator` evalúa reglas activas de BD (`OnInvoiceCreated`, `OnInvoiceOverdue`, `OnStockBelowReorder`) con condiciones y acción `SendEmail`; **tiempo real** vía `RealtimeRuleEvaluator` + handlers `OnLeadStatusChanged`/`OnExpenseApproved` |
| 28 | Sustituir la importación manual de CSV bancario por integración de banca abierta (PSD2) para conciliación en tiempo real | Treasury | ✅ Corregido — `IOpenBankingProvider`, `MockOpenBankingProvider`/`StubOpenBankingProvider`/`ConfigurableOpenBankingProvider`, `OpenBankingProviderRegistration` (Mock en dev, Stub en prod sin credenciales, Configurable auto si hay ClientId+Secret+ApiBaseUrl), `SyncOpenBankingHandler`, `POST /api/treasury/bank-accounts/{id}/sync-open-banking`; tests `OpenBankingProviderRegistrationTests` + `Phase18IntegrationHttpTests` |
| 29 | Payroll: integración real con Sistema RED/Seguridad Social, más allá de los exports TC1/TC2 "orientativos" actuales | Payroll | ✅ Corregido (alcance máximo sin homologación) — `RedSiltraFileBuilder` (registros 01/02/99, 250 chars, ISO-8859-1), `SpanishSocialSecurityNumberValidator` (NAF/CCC módulo 97), `PayrollRedExportValidator`, `GET /api/payroll/export/red`; TC1/TC2 con metadatos empresa/CCC; frontend botón RED; golden tests + `docs/payroll-red-siltra.md`; **no homologado TGSS** (disclaimer obligatorio) |
| 30 | Flujos de aprobación (pedidos de compra o gastos por encima de un umbral, antes de confirmar/contabilizar) | Purchasing/Expenses | ✅ Corregido — `Company.ApprovalThresholdAmount`, estados PO Draft→PendingApproval→Approved/Rejected, handlers MediatR + `PurchaseOrder:Approve`, gastos `submit-for-approval` con umbral, frontend badges/botones en `/purchasing/orders` |
| 31 | Conectar el interceptor de Audit Log (`AuditInterceptor.cs`, ya documentado como código muerto en el catálogo de mock de arriba) — de cara al usuario el sistema aparenta tener auditoría inmutable y hoy no la tiene | Core | ✅ Corregido — `AuditSaveChangesInterceptor` en ErpDbContext + 9 módulos; hash SHA256; `settings/audit-logs` consulta datos reales |

**Ítems 32+: mejoras adicionales de plataforma y producto**, identificadas al
cierre de esta auditoría (no se derivan de un hallazgo puntual del código,
sino de comparar el estado actual contra lo que exigiría un ERP de clase
mundial). Mezcla código/plataforma (32-37) y producto (38-42).

| # | Mejora | Módulos | Prioridad |
|---|---|---|---|
| 32 | Cero tests automatizados en todo el repo — priorizar tests de integración sobre los flujos críticos (facturación, asientos automáticos, aislamiento multi-tenant) antes que cobertura exhaustiva | Todos | 🟡 Ampliado (fase 17+) — ver fila **32 Tests** en «Tabla definitiva» más abajo (recuento verificado jul 2026: **633** unit + **161** integración + **5** arquitectura en `dotnet test Erp.slnx`; **130** Vitest en `npm test -- --run`; E2E Playwright **19** casos); gates CI en `scripts/check-coverage.py`; plan en `docs/testing-strategy.md` |
| 33 | No existe middleware global de manejo de excepciones — cualquier excepción no controlada (incluida la `ValidationException` de FluentValidation recién activada en CRM) se filtra como un 500 crudo sin `ProblemDetails` ni contrato de error consistente | Core | ✅ Corregido — `ExceptionHandlingMiddleware` devuelve `application/problem+json` (400/401/404/500 según tipo) |
| 34 | Aislamiento multi-tenant a un solo nivel de defensa (global query filters de EF Core); añadir Row-Level Security de Postgres como segunda barrera | Core | ✅ Ampliado — **19 tablas** vía `PostgresRlsBootstrap`; `Postgres:RlsEnabled=true` por defecto en `appsettings.json`/`appsettings.Development.json` y `k8s/deployment.yaml` (#70); warning en log si está apagado; test integración `PostgresRlsBootstrapTests` sin falsa aserción |
| 35 | Ninguna de las 19 violaciones de arquitectura de este ADR se detecta automáticamente en CI; añadir tests de arquitectura (tipo NetArchTest: "ningún controller referencia DbContext directamente", "Domain no depende de Infrastructure") para que las reglas se apliquen solas en cada PR | Core/CI | ✅ Ampliado — **5 reglas** en `ControllerArchitectureTests`: DbContext inline, Domain sin EF, `[RequiredModule]` en controllers de módulo, `[RequirePermission]` en acciones HTTP, `Erp.Infrastructure` sin referencias a `*.Modules.*.Application`; exempt públicos anónimos y `FiscalHomologationController`/`StripeWebhookController` |
| 36 | Sin observabilidad real: no hay logging estructurado, tracing distribuido ni métricas en ningún módulo — depurar producción (p. ej. por qué se atascó el outbox) hoy depende de logs de consola sueltos | Core | ✅ Corregido — Serilog + OpenTelemetry (métricas Prometheus `/metrics`, tracing OTLP opcional, collector en compose local) |
| 37 | Frontend con muy poca reutilización de componentes — ver auditoría dedicada y desglose en ítems 43-51 | Frontend | ✅ Corregido — `FormErrorBanner`, `PageListLayout`, `FormLabel`, `EmptyState`, `LoadingPlaceholder` |
| 38 | ~~Multi-moneda real en Billing (facturar en divisa distinta del euro con conversión automática usando los tipos de cambio de Treasury)~~ **✅ Corregido (jul 2026)**: `Invoice.CurrencyCode`/`ExchangeRateToEur`/`TotalEur` + migración `20260705130000_AddInvoiceMultiCurrency` (con atributo `[Migration]`); `CreateInvoiceHandler` resuelve tipos vía `IExchangeRateLookup` → `ExchangeRateLookupAdapter` (Treasury); `MarkPaidHandler`/`InvoiceApprovedEvent` usan `TotalEur` para asientos; selector de divisa en `BillingClient.tsx`; `FacturaEService` usa `invoice.CurrencyCode` en XML (`TaxCurrencyCode` EUR para IVA) | Billing ↔ Treasury | ✅ Corregido |
| 39 | ~~Portal de autoservicio para cliente/proveedor (ver y pagar facturas, subir facturas de proveedor) — hoy todo el flujo es interno, sin reenvío manual de PDFs~~ **✅ Corregido — las 3 piezas (ver factura, pagar factura, subir factura de proveedor)**: `Invoice.PublicViewToken` (mismo patrón que `Quote.AcceptanceToken`), `GetInvoiceByTokenQuery`/`GetInvoiceByTokenHandler` (Billing.Application) e `InvoicePublicDto` reutilizando `PublicInvoiceLineDto`. Nuevo `PublicInvoiceViewController` (`api/v1/public/invoice-view/{token}`, `[AllowAnonymous]`) — no confundir con `PublicInvoicesController` (`api/v1/public/invoices`), que es un endpoint de integración protegido por `X-Api-Key` para todo el ledger del tenant, no un visor por factura. Exención añadida en `PublicApiPaths.RequiresApiKey`. `InvoiceDto.PublicViewUrl` (calculado vía `IPortalUrlProvider`, mismo servicio que ya usaba Quote) expuesto en los 3 sitios donde se construye la respuesta staff (`CreateInvoiceHandler`, `GetInvoicesHandler`, `GetInvoiceByIdHandler`), con botón "Copiar enlace para el cliente" en `InvoiceDetailClient.tsx` — sin esto el enlace no sería descubrible por nadie. Frontend público: `factura/[token]/page.tsx` (mismo estilo que `presupuesto/[token]/page.tsx`). **Bug real encontrado y corregido en la migración generada**: `AddColumn` de EF pone el mismo valor por defecto (`""`) a todas las filas existentes, lo que habría hecho fallar el índice único sobre `PublicViewToken` en cualquier base con más de una factura ya creada — se añadió un backfill SQL (`UPDATE ... SET "PublicViewToken" = replace(gen_random_uuid()::text, '-', '')`) antes de crear el índice. La migración también arrastró de forma incidental dos columnas (`VerifactuRealtimeSubmission`, `VerifactuSubmittedAt`) y una tabla (`VerifactuSubmissionLogs`) que ya existían en el modelo C# pero nunca se habían migrado — mismo patrón que el hallazgo de `Collaterals` en Treasury (#42b). **Segundo bug real encontrado tras el primer cierre**: `PublicApiPaths.RequiresApiKey` tenía la exención pero `TenantResolverMiddleware.IsProtectedRoute()` mantiene su **propia lista separada** de rutas públicas y no incluía `/api/v1/public/invoice-view` — el portal entero devolvía 401 "Tenant ID requerido" en cualquier despliegue real pese a que todos los tests unitarios (que no pasan por el pipeline HTTP) pasaban. Corregido añadiendo la ruta a esa segunda lista, verificado con un nuevo test end-to-end real (`PublicInvoicePortalEndToEndTests`, `Erp.IntegrationTests`, vía `InMemoryErpWebApplicationFactory`) que sí atraviesa el middleware — cualquier ruta pública nueva debe comprobar **tres** sitios: `PublicApiPaths.RequiresApiKey`, `TenantResolverMiddleware.IsProtectedRoute`, y el proxy Next.js (cubierto automáticamente si la ruta usa el prefijo versionado `api/v1/public/...`). **Pago**: mismo `PublicInvoiceViewController` expone `POST {token}/checkout` → `CreateInvoiceCheckoutSessionCommand` (valida `IsLocked`/no ya pagada) → `IInvoicePaymentGateway` (nueva interfaz, implementada por `StripeService`) crea una sesión Stripe Checkout `Mode = "payment"` por el importe exacto — sin tocar el flujo de suscripción SaaS existente (Price ID fijo, producto Stripe distinto). El webhook publica `StripeInvoiceCheckoutCompletedEvent` (evento de dominio en `Erp.Application.Common.Events`) en vez de que `Erp.Infrastructure` llame directamente a `MarkPaidCommand` — evita repetir la violación de dependencias ya documentada arriba (core→Application de módulo); `MarkInvoicePaidFromStripeHandler` (Billing.Application) consume el evento y reenvía al `MarkPaidCommand` idempotente existente. `MarkPaidHandler` necesitó pasar a `IgnoreQueryFilters()` (mismo motivo que `GetInvoiceByTokenHandler`: el webhook no tiene tenant resuelto). 5 tests nuevos (`CreateInvoiceCheckoutSessionHandlerTests`, `MarkInvoicePaidFromStripeHandlerTests`) + los 3 de `GetInvoiceByTokenHandlerTests` + 2 E2E. **Subida de factura de proveedor**: `Supplier.PublicUploadToken`/`PublicUploadEnabled` (mismo patrón que `Company.PublicUploadToken` de gastos) + `SupplierInvoiceUpload` (CRM, no Purchasing — el token vive en `Supplier` y Purchasing no tiene referencia a `Crm.Application`, evita crear esa dependencia solo para validar un token) + nuevo `PublicSupplierUploadController` (`api/v1/public/supplier-uploads/{token}`, `[AllowAnonymous]`, mismo validador MIME+magic-bytes que `ExpensesController.Upload`). Exención añadida en los mismos tres sitios que el hallazgo de arriba (`PublicApiPaths.RequiresApiKey`, `TenantResolverMiddleware.IsProtectedRoute`; el proxy Next.js queda cubierto automáticamente por el prefijo `v1/public`). Revisión del staff vía `SuppliersController` (`GET uploads`, `GET uploads/{id}/download-url`, `POST uploads/{id}/mark-reviewed`) — la conversión a un `SupplierInvoice` real de Purchasing sigue siendo manual. Frontend: `proveedor/[token]/page.tsx` (subida anónima), `purchasing/supplier-uploads/page.tsx` (revisión), toggle+copiar-enlace en `SuppliersClient.tsx` (que además corrigió un bug real preexistente: el modal de ficha de proveedor leía `selected.supplier.*` cuando el backend siempre devolvió el DTO plano — `selected.*` —, lo que habría lanzado un `TypeError` en cualquier uso real del modal). 12 tests nuevos (`SupplierInvoiceUploadHandlersTests`, `SupplierPublicUploadFieldsTests`) | Billing/Purchasing | ✅ Corregido |
| 40 | ~~Funciones asistidas por IA…~~ **✅ Corregido jul 2026** — `GetExpenseAnomaliesQuery`, `SuggestExpenseCategoryQuery`, `GET /api/expenses/anomalies|suggest-category`, `GET /api/treasury/liquidity-forecast` (30/60/90), UI `ExpensesClient` + `TreasuryClient`; heurísticas sin LLM | Expenses/Treasury | Baja |
| 41 | ~~Asistente de alta/onboarding…~~ **✅ Corregido jul 2026** — `OnboardingController`, sectores PGC, `ImportOnboardingClientsCommand`, UI `settings/onboarding` | Core | Baja |
| 42 | ~~Notificaciones proactivas…~~ **✅ Corregido jul 2026** — `ProactiveNotificationsJob` (Hangfire 8:00), `Company.ProactiveNotificationsFrequency`, UI `settings/notifications`; reutiliza `RuleEvaluatorJob` + aprobaciones PO/gastos | Core/Automatización | Media |
| 42a | **Multi-empresa real (un mismo login con acceso a varias `Company`)**: hoy el modelo es 1 `User` = 1 `Company` de forma rígida (`User.CompanyId` es una FK única, sin tabla `UserCompany`), email único a nivel de toda la plataforma (`RegisterCompanyCommand.cs:54-56`), y `CompanyId` grabado como claim fijo e inmutable en el JWT al hacer login (`JwtProvider.cs:33`) — no hay ningún concepto de "empresa activa" cambiable en sesión. Esto bloquea el caso de uso más relevante para el público pyme/gestoría al que apunta el producto: una gestoría que lleva la contabilidad de varias pymes clientas necesitaría hoy una cuenta (email+contraseña) distinta por cada empresa cliente, sin ninguna vista ni cambio de contexto conjunto. Requiere: tabla `UserCompany` (muchos-a-muchos, con rol por membresía) en sustitución/complemento de `User.CompanyId`; relajar la unicidad de email a `(Email, CompanyId)`; JWT/sesión con lista de empresas accesibles + "empresa activa" cambiable sin re-login (el `TenantContext.tsx` del frontend ya tiene `setTenant()`/`clearTenant()` pero hoy nadie los invoca — sería la pieza a activar); alta self-service de "otra empresa" desde una cuenta ya existente (hoy crear `Company` solo lo hace el admin raíz vía `settings/empresas` o el registro inicial); y decidir el modelo de suscripción (¿un plan que cubra N empresas para gestorías, en vez de 1 suscripción por `Company` como hoy?). **Importante para el orden de implementación**: no construir esto reutilizando las tablas de consolidación de grupo de Treasury (`ConsolidationGroup`/`SubsidiaryCompany`, ítem 15) — están mock y no tienen ninguna relación con `User`/auth; son un problema distinto (consolidación contable de un grupo empresarial) que solo tiene sentido abordar después de que exista multi-empresa real a nivel de acceso | Core (User/Company/Auth) | 🟢 **Sustancialmente hecho — más avanzado de lo que decía esta fila ("Fase 1")**: verificado directamente que `UserCompany` (muchos-a-muchos con rol) existe, `SwitchCompanyHandler` valida membresía y **regenera el JWT con la nueva empresa activa** (`JwtProvider.Generate(user, companyId)`), `AddCompanyFromAccountHandler` da de alta una empresa nueva desde una cuenta existente sin crear otro `User` (justo lo que esta fila pedía), y `CompanySwitcher.tsx` llama de verdad a `auth/companies`/`auth/switch-company` y reescribe las cookies de sesión. Con tests (`SwitchCompanyHandlerTests`, `AddCompanyFromAccountHandlerTests`). Lo único que falta de lo listado originalmente: el modelo de suscripción para gestorías (un plan que cubra N empresas en vez de 1 suscripción por `Company`) — eso sigue siendo una decisión de negocio pendiente, no un hueco técnico |

| 42b | ~~Conciliación de ingresos por método de pago (TPV, transferencia, Bizum, caja) — `PaymentMethod` string libre sin restricción, solo distinción binaria efectivo/banco en el asiento, sin entidad de arqueo de caja~~ **✅ Corregido**: `PaymentMethods` (`Modules/Billing/Application/Features/Billing/PaymentMethods.cs`) restringe a `cash\|bank\|card\|bizum\|transfer` (guard clause inline en `MarkPaidHandler` — Billing no registra `AddValidatorsFromAssembly`, un validador ahí no se ejecutaría). `PaymentReceivedEventHandler` enruta por método: `cash`→"570", `card`→"5721" (TPV pendiente de liquidar), `bizum`→"5722" (Bizum pendiente de liquidar), `bank`/`transfer`→"572" — sembradas por `SeedChartOfAccountsHandler` (#0g). `BankAccount` (Treasury) expone `AccountingAccountCode` en alta y tabla (`TreasuryClient.tsx`), así que ya se puede crear una cuenta "TPV"/"Bizum" y conciliarla con `BankReconciliationService` (sin cambios, ya filtra por prefijo). **Arqueo de caja**: nueva entidad `CashSession` (`Modules/Treasury/Domain/Entities/`, migración `AddCashSessions`) con apertura (`OpenCashSessionCommand`, importe inicial) y cierre (`CloseCashSessionCommand`, importe contado) vía `CashSessionsController` (`api/treasury/cash-sessions`). El importe esperado se calcula reutilizando `IBankReconciliationLedgerQuery` (el mismo puerto que ya usa `BankReconciliationService`, sin inyectar `IAccountingDbContext` de nuevo) sobre los movimientos reales de la cuenta "570" desde la apertura. Si hay diferencia, Treasury publica `CashSessionClosedEvent` (no crea el asiento directamente) y `PostCashDifferenceHandler` en Accounting registra el ajuste (sobra: Debe 570/Haber 778 "Ingresos excepcionales"; falta: Debe 668 "Otras pérdidas en gestión corriente"/Haber 570 — cuentas nuevas en `SeedChartOfAccountsHandler`). Frontend: pestaña "Arqueo de Caja" en `TreasuryClient.tsx` (abrir/cerrar + histórico). **Hallazgo adicional corregido de paso**: al generar la migración de `CashSessions` se detectó que la entidad `Collateral` (Treasury) nunca había tenido migración — la tabla `treasury.Collaterals` no existía en ninguna migración previa pese a usarse en `GuaranteeHandlers.cs`; la nueva migración la crea también. 24 tests nuevos (`PaymentReceivedEventHandlerTests`, `MarkPaidHandlerTests`, `CashSessionHandlerTests`, `PostCashDifferenceHandlerTests` — primeros tests de Treasury en el repo) | Billing ↔ Treasury | ✅ Corregido |
| 42c | ~~Cruce entre módulo contratado y permiso de usuario...~~ **✅ Corregido — y el hallazgo real era mayor que lo descrito en esta fila**: al investigar se confirmó que `[RequiredModule]` solo se aplicaba en 3 de 44 controllers (los 3 de Inventory) y `[RequirePermission]` en 0 de 44 — y que **ninguno de los dos habría funcionado en producción de todas formas**: `ModuleAuthorizationFilter` estaba registrado en `Program.cs` únicamente como `IAsyncAuthorizationFilter` (interfaz), nunca como tipo concreto, así que `options.Filters.AddService<ModuleAuthorizationFilter>()` no podía resolverlo — el atributo era código muerto incluso donde se usaba. Corregido en 4 frentes: (1) `ModuleAuthorizationFilter` registrado como tipo concreto en `Erp.Infrastructure/DependencyInjection.cs` y añadido a `options.Filters` en `Program.cs` junto a `AbacAuthorizationFilter`; (2) catálogo ABAC ampliado en `Permissions` (`RequirePermissionAttribute.cs`) de 7 a ~35 grupos de recurso cubriendo los 9 módulos de negocio (convención: Create/Read/Update/Delete + `Approve` + `Export` + `Manage`, documentada en el propio archivo); (3) `[RequiredModule]`+`[RequirePermission]` en los **49 controllers de módulo de negocio** (los 4 controllers públicos anónimos — `PublicInvoiceViewController`, `PublicInvoicesController`, `PublicQuotesController`, `PublicSupplierUploadController` — quedan exentos a propósito; los ~18 controllers core de `Erp.Api` — Auth, Admin, Platform, Sii, Tax, etc. — no llevan `[RequiredModule]` porque no son endpoints de un módulo licenciado); (4) descubierto y corregido un segundo bug bloqueante: ninguna empresa real (solo el bootstrap de dev) recibía nunca filas `TenantModule`/`RolePermission`, así que activar la comprobación habría bloqueado a todo el mundo — nuevo `SeedTenantModulesHandler` (activa los módulos incluidos en el plan) y `SeedDefaultRolePermissionsHandler` (Admin=todo, Manager=todo salvo `UserManagement:Delete`/`Accounting:Close`, Contable=acceso completo a recursos financieros + solo lectura al resto), ambos `INotificationHandler<CompanyCreatedEvent>` igual que `SeedChartOfAccountsHandler` (#0g); también se añadieron los 4 módulos (Treasury/Payroll/Purchasing/Sales) que nunca habían tenido fila `PlanModule` en ningún plan. **Refuerzo jul 2026 (commit `0513640`)**: se añadieron atributos ABAC a 6 controllers fiscales/contables que el merge #3c había dejado sin permisos granulares — permisos reutilizados del catálogo existente, sin nuevos recursos: `AeatModelsController` (`Vat.Read`/`Vat.Manage`/`Accounting.Export`), `IvaManagementController` (idem), `InversionSujetoActivoController` (`Vat.Read`/`Vat.Manage`), `AgingController` y `FinancialStatementsController` (`FinancialStatement.Read` a nivel de clase), `FacturaEController` (`FacturaE.Read`/`FacturaE.Export`/`FacturaE.Manage`). `SeedDefaultRolePermissionsHandler` ya sembraba esos recursos en el rol Contable vía `FinancialResources`. **Corrección (contra-auditoría jul 2026, re-verificado):** `ModuleApiControllers_HaveRequiredModuleAttribute` y `ModuleApiControllers_HaveRequirePermissionOnActions` **sí existen** en `Erp.ArchitectureTests/ControllerArchitectureTests.cs` (4 `[Fact]` en total, incluyendo DbContext y Domain); `dotnet test Erp.ArchitectureTests` pasa en verde. Los 15 tests de seeding citados abajo también existen. 15 tests nuevos (`SeedTenantModulesHandlerTests`, `SeedDefaultRolePermissionsHandlerTests`, `ModuleAuthorizationFilterRegistrationTests`) — dos de ellos detectaron un bug real propio (los handlers nuevos leían `Roles`/`Subscriptions`/`TenantModules` sin `IgnoreQueryFilters()`, así que durante el alta de una empresa —antes de que exista un tenant resuelto— habrían no hecho nada, silenciosamente). **Pendiente, fuera de alcance de este cierre**: los sub-recursos de `TreasuryController` (efectos/órdenes de pago/previsiones) se agrupan provisionalmente bajo el permiso `BankAccount:*` en vez de tener recursos propios; dividir `Manage` en acciones más granulares por recurso si un caso de uso real lo exige | Core (Licensing + Auth) | ✅ Corregido |
| 42d | Biblioteca interna de documentos — ~~no existe ninguna entidad de documento genérica hoy~~ **✅ Corregido**: entidad `Document` (`Erp.Domain/Entities/Core/Document.cs`, `CompanyId` + `EntityType`/`EntityId` genérico opcional, mismo patrón que `PaymentOrder.SourceType`/`SourceId` de Treasury — evita que el core tenga que referenciar módulos), `IApplicationDbContext.Documents` + query filter multi-tenant, migración `AddDocumentTable`. CQRS completo en `Erp.Application/Features/Documents/` (`UploadDocumentCommand`, `DeleteDocumentCommand`, `GetDocumentsQuery` paginado, `GetDocumentDownloadUrlQuery`) vía `DocumentsController` (`api/documents`, delgado, solo `IMediator`). Reutiliza `IFileStorageService`/MinIO ya existente (bucket `erp-documents`, autocreado). Frontend real y conectado: `settings/documents` (Server Component + Client, enlazado en `Sidebar.tsx`) — subida por `FormData`, descarga vía URL firmada, borrado. **Hallazgo adicional corregido de paso**: el proxy genérico de Next.js (`api/proxy/[...path]/route.ts`) forzaba `Content-Type: application/json` y leía el body como texto para cualquier POST/PUT/PATCH — habría roto cualquier subida `multipart/form-data` (no solo la de este ítem); ahora detecta multipart y reenvía el body y el `Content-Type` (con boundary) tal cual. Tests: 6 casos en `Erp.Tests/Documents/DocumentHandlersTests.cs` (subida válida, tipo no permitido, contenido vacío, filtro por entidad, borrado, borrado cross-tenant rechazado) con `FakeFileStorageService` nuevo en `TestSupport/` | Core | ✅ Corregido |
| 42e | ~~Envío periódico de documentos a gestorías externas~~ **✅ Corregido (jul 2026)**: ZIP completo — libros IVA (CSV, filtrados por periodo), asientos (`asientos/diario.csv`), PDFs facturas emitidas bloqueadas (`facturas/`), documentos gastos (`gastos/`), `LEEME.txt` con índice. Handlers vía MediatR; exporters reutilizan `GetInvoicePdfQuery` y `ILibroIva*Exporter` con `FiscalExportPeriod`. Tests: `ExportAccountantPackageHandlerTests` | Billing/Accounting | ✅ Corregido |
| 42f | ~~Servicios contratados por cliente... no existe ningún concepto de esto hoy~~ **✅ Corregido**: catálogo de servicios por empresa (`ServiceCatalogItem`, `Modules/Crm/Domain/Entities/ServiceCatalogItem.cs` — CompanyId, precio/IVA/periodicidad por defecto, se desactiva en vez de borrarse) y contrato por cliente (`ClientContractedService`, snapshot de nombre/precio/IVA/periodicidad al firmar, igual que el snapshot fiscal de `Invoice`). CQRS en `Erp.Modules.Crm.Application/Features/Services/` (catálogo y contratos), controllers delgados (`ServiceCatalogController` en `api/service-catalog`, sub-acciones `{id}/contracted-services` en `ClientsController`, mismo patrón que `/anonymize`). Facturación recurrente resuelta sin acoplar Crm↔Billing por referencia de proyecto: `ContractedServiceBillingJob` (Crm.Infrastructure, Hangfire diario 2:00) publica `ClientServiceDueForBillingEvent`; `GenerateRecurringServiceInvoiceHandler` (Billing.Application) lo consume y llama al `CreateInvoiceCommand` real (cero lógica de facturación duplicada) y publica `RecurringServiceInvoiceGeneratedEvent`; `AdvanceContractedServiceBillingHandler` (Crm.Application) avanza `NextBillingDate`/`LastInvoiceId` — los tres eventos viven en `Erp.Application.Common.Events`, el núcleo ya compartido por todos los módulos, sin crear una referencia de proyecto nueva. El asiento contable no requirió tocar Accounting: `PaymentReceivedEventHandler` ya se dispara al cobrarse la factura generada. Guard clauses inline en los handlers en vez de FluentValidation (el pipeline de validación no se ejecuta para módulos, ver ítem de FluentValidation muerto más abajo). Tests: `tests/Erp.Tests/Crm/` (primer test de CRM en el repo) + `tests/Erp.Tests/Billing/GenerateRecurringServiceInvoiceHandlerTests.cs`, con `FakePublisher`/`FakeMediator` nuevos en `TestSupport/`. Frontend conectado de verdad: `crm/services` (RSC + `AccessibleModal`) para el catálogo, sección "Servicios contratados" en `crm/clients/[id]` para altas/cancelaciones por cliente | Billing ↔ Crm | ✅ Corregido |
TypeScript + Tailwind), mismo nivel de detalle que la auditoría de backend,
con cita de archivo para cada hallazgo — amplía y sustituye al ítem 37.

| # | Mejora | Evidencia | Prioridad |
|---|---|---|---|
| 43 | ~~71 de 72 `page.tsx` son Client Components~~ **✅ Corregido**: las 13 páginas hub/lista que quedaban 100% cliente ya están convertidas al patrón Server Component + Client Component (`serverFetch`/`serverFetchList` en `page.tsx`, estado/JSX en `XxxClient.tsx`) — `admin`, `crm`, `sii`, `settings`, `settings/sii`, `treasury`, `verifactu`, `purchasing/orders/[id]` y los 6 hubs de `accounting` (`page`, `iva-registers`, `cash-flow`, `depreciation`, `cierre`, `budgets`). Quedan deliberadamente client-only (decisión, no hueco): los 6 formularios `*/new` de compras/ventas y las 4 páginas auth/públicas (`register`, `signup`, `settings/add-company`, `presupuesto/[token]`) — no tienen un GET inicial que mover al servidor | Patrón idéntico en los 9 módulos revisados, p. ej. `sales/orders/page.tsx:24-40`, `treasury/currencies/page.tsx:16-36` | ✅ Corregido |
| 44 | Fugas de `any` pese a `strict: true` en `tsconfig.json` | Frontend | ✅ Corregido — 0 `any` en `app/`; solo `Record<string, unknown>` en `types/api.ts` (OCR) |
| 45 | Sin boundaries de error; `alert()` nativo | Frontend | ✅ Corregido — 0 `alert()` en `src/`; banners inline en todos los módulos |
| 46 | **Proxy abierto de facto**: `api/proxy/[...path]/route.ts` define `PROXY_PATHS` como aparente whitelist pero nunca se usa — cualquier request autenticada se reenvía a `${backendUrl}/api/${path}` para cualquier ruta del backend, sin restricción real | `src/app/api/proxy/[...path]/route.ts` — ahora `ALLOWED_PATH_PREFIXES` se valida con `isPathAllowed()` antes de reenviar; rutas no listadas devuelven 403 | ✅ Corregido |
| 47 | Bug real en el proxy: en `proxyFetch`, `response` se declara dentro del `try` pero el `catch` la referencia (`response.headers.get(...)`) — si el `fetch` falla (backend caído, DNS), el catch lanza `ReferenceError` en vez de devolver el JSON de error esperado | `src/app/api/proxy/[...path]/route.ts` — el `catch` ya no referencia `response`; devuelve 502 con mensaje de conexión | ✅ Corregido |
| 48 | ~~Accesibilidad mínima: 0 atributos `aria-*`... 0 `role="dialog"` en los 8+ modales existentes~~ **✅ Corregido**: los 4 modales hechos a mano que quedaban (`sii/page.tsx`, `settings/sii/page.tsx`, `fiscal/FiscalClient.tsx`, `inventory/InventoryClient.tsx`) ya usan el componente compartido `AccessibleModal` (`role="dialog"`, `aria-modal`, cierre con Escape, foco inicial) — 0 archivos con `modal-overlay` sin `AccessibleModal` en todo `app/` (verificado por grep) | `crm/prospects/page.tsx:272`, `crm/leads/page.tsx:134`, `crm/clients/[id]/page.tsx:125-139` | ✅ Corregido |
| 49 | ~~Sin caché ni estado compartido~~ **✅ Corregido (alcance decidido)**: `useCachedApi` ya cubre los hubs revisados con frecuencia — `crm/page`, `billing`, `billing/quotes`, `crm/alerts`, `crm/clients`, `crm/contacts`, `crm/leads`, `crm/suppliers`, `expenses`, `inventory`, y ahora también `purchasing/orders`, `purchasing/invoices`, `sales/orders`, `sales/invoices`, `sales/deliveries`, `settings/users` (mutaciones invalidan caché antes de refetch). Decisión explícita: el resto de páginas encontradas sin `useCachedApi` (páginas de detalle `[id]/...Client.tsx`, formularios `*/new`, configuraciones de un solo uso como `accounting/aeat`, `treasury/currencies`) NO se convierten — son fetches de un solo uso o de baja frecuencia de revisita donde una caché TTL no aporta valor real; no es un hueco pendiente | `hooks/useApi.ts`, `lib/api.ts` (código muerto); `crm/page.tsx`, `crm/alerts`, `billing/quotes`, etc. | ✅ Corregido (alcance acotado a hubs de alta revisita) |
| 50 | ~~0 usos de `useMemo`/`React.memo` en todo `app/`~~ **✅ Corregido (alcance decidido)**: `useMemo` cubre las listas filtradas/ordenadas de los hubs de alta revisita — `crm/page` (ahora `CrmHomeClient`), `fiscal/FiscalClient`, `treasury` (KPIs, ahora `TreasuryClient`), y los mismos 6 hubs de `purchasing`/`sales`/`settings/users` del ítem 49. El resto de páginas con `.filter()`/`.sort()` en el cuerpo de render (detalle, `*/new`, configuraciones de un solo uso) se deja sin memoizar deliberadamente: son arrays pequeños o de un solo cálculo por carga, memoizarlos sería optimización prematura sin beneficio medible — no es un hueco pendiente | `crm/page.tsx:77-79,91` | ✅ Corregido (alcance acotado a hubs de alta revisita) |
| 51 | Sin librería de formularios/validación (0 uso de react-hook-form/Zod/Formik) — "validación" es solo comprobar campos no vacíos vía `if`+`alert()`, sin reflejar las reglas reales del backend (FluentValidation); mismo patrón superficial repetido en 18+ formularios | `crm/clients/[id]/page.tsx:57-60`, `treasury/currencies/page.tsx:39`, y 16 archivos más | ✅ Corregido (flujos new/*) — Zod en los 6 formularios `new/*` de compras/ventas + auth + add-company + editores CRM |

Nota positiva de la misma auditoría: `globals.css` sí tiene un sistema de
clases reutilizable (`.btn-primary`, `.erp-input`, `.erp-card`, etc.,
usado en 54 de 78 archivos), la configuración de URL de API vía
`NEXT_PUBLIC_API_URL`/`API_URL` es correcta sin URLs hardcodeadas de
producción, y `package.json` no arrastra dependencias pesadas innecesarias
— la base es más sana de lo que el ítem 37 original sugería; el problema es
consistencia y patrones, no herramientas equivocadas.

**Ítems 52-64: auditoría dedicada de infraestructura** (Docker Compose, k8s,
CI/CD, nginx, backups), mismo nivel de evidencia que backend/frontend. A
diferencia de las anteriores, aquí varios hallazgos son **bugs confirmados
en producción potencial**, no solo deuda de diseño — se marcan explícitamente.

> **Actualización de contexto (post-auditoría): ya no hay VPS.** El servidor
> Hetzner sobre el que se diseñó originalmente este despliegue ya no existe.
> El objetivo actual, hasta nuevo aviso, es **Docker Compose en local con
> base de datos también en local** (contenedor Postgres, sin servidor
> remoto) — el despliegue en servidor real se retoma más adelante. Los
> ítems marcados **"Aparcado"** son específicos del VPS (TLS/Let's Encrypt,
> firewall, backups offsite, script de aprovisionamiento) y no tienen
> cabida ahora mismo; se quedan documentados para cuando se retome esa
> parte, no se eliminan. Los que siguen aplicando a un entorno Docker local
> se mantienen con su prioridad normal.

| # | Mejora | Evidencia | Prioridad |
|---|---|---|---|
| 52 | Desajuste de puerto 5000 vs. 8080 real — **no era solo un problema del script de servidor**: `.env.example` traía `ASPNETCORE_URLS=http://+:5000`, que también rompía el arranque local (el contenedor expone/healthchequea 8080). ✅ Corregido: `.env.example` usa `8080`; URLs frontend `8081`/`8080` (ítem 65); **`deploy/deploy.sh` y job `deploy` en `ci-cd.yml` usan `:8080/health/live`** (jul 2026); CI falla si healthcheck no responde | `deploy/deploy.sh`, `.github/workflows/ci-cd.yml`, `backend/Dockerfile`, `.env.example` | ✅ Corregido |
| 53 | TLS desactivado en nginx de producción con HSTS activo (footgun) — solo aplica cuando haya un dominio/servidor real sirviendo HTTPS | `deploy/nginx/erp.conf:5,8-10,16` | Aparcado — sin servidor |
| 54 | Nginx de host y de contenedor compitiendo por 80/443 — parte de `setup-vps.sh`, no del flujo local | `deploy/setup-vps.sh:20,49-50` + `docker-compose.yml:88-89` | Aparcado — sin servidor |
| 55 | ~~Postgres publicado a `0.0.0.0:5432` sin bloqueo de firewall — `docker-compose.prod.yml` no tocaba el servicio `postgres`, así que el `ports: "5432:5432"` de la base seguía activo también en el stack de producción~~ **✅ Corregido** — `docker-compose.prod.yml` ahora tiene `postgres: ports: !override []`; verificado con `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` que no publica ningún puerto en modo producción (en local sigue publicado en 5432, sin cambios) | `docker-compose.yml:16-17`, `docker-compose.prod.yml:17-18` | ✅ Corregido |
| 56 | Backups solo en disco local sin copia offsite — no aplica sin un servidor que respaldar; en local el propio equipo del usuario es el "backup" | `deploy/backup.sh:32-40` | Aparcado — sin servidor |
| 57 | El pipeline de CI/CD nunca hace escaneo de vulnerabilidades (dependencias ni imagen de contenedor) — sigue teniendo sentido aunque no haya despliegue activo, es higiene de repositorio | `.github/workflows/ci-cd.yml` | ✅ Corregido — `dotnet list package --vulnerable` + `npm audit --audit-level=high` |
| 58 | Deploy sin estrategia zero-downtime (`docker compose down` completo) — solo relevante para el script de servidor | `deploy/deploy.sh:16-18` | Aparcado — sin servidor |
| 59 | Dos workflows de CI/CD divergentes; imágenes que CI construye nunca llegan a desplegarse — vale la pena simplificar a un único workflow aunque hoy no despliegue a ningún sitio, para no arrastrar confusión cuando se retome servidor | `.github/workflows/ci-cd.yml` (ambos), `docker-compose.yml` | ✅ Corregido — un solo workflow; job deploy Hetzner eliminado (VPS aparcado) |
| 60 | Sin monitorización externa (logs/alertas/uptime) — solo tiene sentido con algo desplegado que monitorizar | — | Aparcado — sin servidor |
| 61 | Renovación de certificados Let's Encrypt no automatizada — no aplica sin dominio/servidor real | `deploy/setup-vps.sh:50` | Aparcado — sin servidor |
| 62 | El manifiesto de k8s está obsoleto y nunca se ha usado — al no haber servidor tampoco hay presión por mantenerlo; valorar si retirarlo del repo o dejarlo como referencia futura | `k8s/deployment.yaml` completo | Baja |
| 63 | Sin infraestructura como código; `setup-vps.sh` no es re-ejecutable de forma segura — irrelevante mientras no haya VPS que aprovisionar | `deploy/setup-vps.sh` completo | Aparcado — sin servidor |
| 64 | Todo pensado para un único VPS sin redundancia — la pregunta de redundancia/HA no aplica a un entorno Docker local de desarrollo | `docker-compose.yml` completo | Aparcado — sin servidor |
| 65 | Verificar que `docker compose -f docker-compose.yml -f docker-compose.local.yml up` levanta el stack completo en local. ✅ Corregido: `docker compose up -d --build` OK (5 servicios); `GET :8081/health` → 200 Healthy (postgres + redis). Bugs corregidos en esta verificación: `totalBalance` duplicado en `treasury/page.tsx` (rompía `npm run build` del frontend) y carpeta `Modules/Inventory/API` con casing incorrecto (solo falla en Linux/Docker; Windows lo enmascara) | `docker-compose.yml`, `docker-compose.local.yml`, `frontend/src/app/treasury/page.tsx`, `backend/Modules/Inventory/Api/` | ✅ Corregido |

**Detalle de la verificación del ítem 65 (bugs reales encontrados y corregidos):**

1. **`backend/Dockerfile` no compilaba.** Solo copiaba los 4 `.csproj` del
   core antes de `dotnet restore "Erp.Api/Erp.Api.csproj"`, pero
   `Erp.Api.csproj` tiene ~30 `ProjectReference` hacia
   `backend/Modules/*/*.csproj` (monolito modular) — el restore fallaba
   porque no podía resolver esas referencias. ✅ Corregido en dos pasos:
   primero copiando todo `backend/` antes de restaurar (funcional, pero
   invalidaba la caché de restore en cada cambio de `.cs`); después
   optimizado con un stage `csproj-only` que aísla solo los `.csproj`
   (preservando su estructura de carpetas vía `find ... -delete`) antes del
   stage de `build`, para que la capa de `dotnet restore` solo se invalide
   cuando cambian referencias/paquetes, no en cada edición de código.
   Verificado con `docker build --target build` real (build completo hasta
   `/app/publish/Erp.Api.dll`) y verificado explícitamente el efecto de la
   caché: tras tocar solo un `.cs` (`Erp.Api/Program.cs`) y reconstruir, el
   paso `RUN dotnet restore` sale `CACHED` en el log de BuildKit — confirma
   que el stage `csproj-only` produce contenido idéntico cuando no cambia
   ningún `.csproj`, y BuildKit reutiliza esa capa aunque el `.cs` cambiara.
2. **8 páginas del frontend con encoding roto (Windows-1252/ISO-8859 en vez
   de UTF-8) rompían el build de producción entero.** `npm run build`
   (Turbopack) fallaba con "Reading source code for parsing failed...
   invalid utf-8 sequence" en `accounting/aeat-models`, `cost-centers`,
   `isp`, `iva-registers`, `prorrata`, `recargo`, `vat-regime` y
   `billing/facturae`. ✅ Corregido con `iconv -f WINDOWS-1252 -t UTF-8`;
   verificado con `npm run build` completo (68 rutas generadas,
   `.next/standalone/server.js` presente, que es lo que consume
   `frontend/Dockerfile`).
3. **`next.config.js` y `next.config.ts` coexistían** con contenido
   solapado; solo el `.ts` tenía `output: 'standalone'` (imprescindible para
   el Dockerfile multi-stage). ✅ Corregido: se elimina el `.js`, se
   consolida todo en `next.config.ts` (de paso se quita la clave `eslint`,
   que Next.js 16 ya no soporta en `next.config` y generaba un warning).
4. **Bug de semántica de Compose en `docker-compose.local.yml`**: el propio
   comentario del archivo afirmaba que el override quitaba el mount SSL y
   el puerto 443 de nginx, pero Compose fusiona listas (`ports:`,
   `volumes:`) por `target` en vez de reemplazarlas — verificado con
   `docker compose config` que ambos seguían presentes pese al override.
   ✅ Corregido usando el tag `!override` de la Compose Specification en
   `ports:`/`volumes:` del servicio `nginx`; re-verificado con
   `docker compose config` que el nginx local queda solo con puerto 80 y
   sin mount SSL.
5. **`.env.example` tenía `ASPNETCORE_URLS=http://+:5000`**, en conflicto
   con el puerto real 8080 del contenedor (ítem 52). ✅ Corregido.
6. **Footgun de contraseña duplicada**: `.env.example` pedía pegar la
   contraseña de Postgres dos veces (una en `POSTGRES_PASSWORD`, otra
   sustituida a mano dentro de `DATABASE_URL`, ya que Compose no
   interpola `${VAR}` escritas dentro del propio `.env`). ✅ Corregido:
   `docker-compose.yml` construye `ConnectionStrings__DefaultConnection`
   directamente a partir de `${POSTGRES_DB}`/`${POSTGRES_USER}`/
   `${POSTGRES_PASSWORD}` (que Compose sí interpola al estar en el propio
   compose file); se elimina `DATABASE_URL` de `.env.example`.
7. El mount `/opt/erp/certs` (ruta absoluta del VPS) se remapea en
   `docker-compose.local.yml` a `./deploy/certs` (repo-local, carpeta nueva
   con `.gitkeep`); se confirmó leyendo
   `Erp.Infrastructure/DependencyInjection.cs:75-92` que el backend arranca
   igual sin certificado SII (carga condicional con `File.Exists`), así que
   no hace falta ningún certificado de prueba para el flujo local.
8. **`treasury/page.tsx` definía `totalBalance` dos veces** (refactor
   `useMemo` incompleto): `npm run build` del frontend fallaba en Docker con
   "defined multiple times". ✅ Corregido: un solo `useMemo` que suma solo
   cuentas activas; verificado con `npm run build` (72 rutas) y
   `eslint src/app/treasury/page.tsx` (0 errores).
9. **Carpeta `Modules/Inventory/API` con casing incorrecto** (resto de
   módulos usan `Api/`): en Windows el build local pasaba, pero en Linux
   (Docker) `Erp.Api.csproj` no resolvía `../Modules/Inventory/Api/...` y el
   backend no compilaba. ✅ Corregido renombrando a `Api/`; verificado con
   `docker build --target build` y `docker compose up -d --build` completo.

**Verificación final (jul 2026, máquina con Docker Desktop activo):**
`docker compose -f docker-compose.yml -f docker-compose.local.yml up -d
--build` levanta los 5 servicios; `GET http://localhost:8081/health` devuelve
200 con postgres y redis Healthy.

**Limitación anterior (superada):** en el sandbox inicial de esta
verificación, las imágenes base no se pudieron descargar (Docker Hub bloqueado
por proxy) y Docker Desktop no estaba en ejecución. Esa limitación ya no aplica
en el entorno local verificado arriba.

**Nota sobre el estado real del pipeline:** `git log --oneline main` muestra
21 commits, todos `docs:`/`fix:`/`chore:` sobre ADRs y código de aplicación
— ninguno toca `docker-compose.yml`, `deploy/`, `.github/workflows/` ni
`k8s/`. No hay evidencia de que el pipeline de CI/CD, los scripts de deploy
o el manifiesto de k8s se hayan ejercitado nunca contra un cambio real desde
`FirstVersion` — los bugs de los ítems 52-54 y 59 podrían llevar ahí sin que
nadie los haya visto en acción todavía.

Los ítems 13 y 14 son los de mayor riesgo/alcance dentro de la deuda de
código (tocan la dirección de dependencias del monolito modular entero) y
deberían abordarse solo después de validar los anteriores, con más contexto
y posiblemente en su propia rama/PR dedicado — no como parte de este barrido
incremental. Los ítems 20-42 son de otra naturaleza (funcional/plataforma,
no solo deuda de código puntual) y requieren decisión de producto/negocio
antes de empezar a implementar, no solo luz verde técnica.

**Criterio de cierre de este backlog:** completar los 51 ítems no es
solo "que compile y pase el checklist de arquitectura" — el frontend tiene
que quedar **conectado de verdad al backend real, sin excepciones**. Hoy hay
páginas enteras que aparentan funcionar pero no llaman a ningún endpoint
(`billing/facturae`, `accounting/iva-registers`, `settings/automation`; ver
catálogo de mock más abajo) y botones sin `onClick`. Ningún ítem de este
backlog se da por cerrado si, tras corregirlo, la página correspondiente
sigue sin `fetch`/`onClick` real contra su endpoint, o si el endpoint que
llama sigue devolviendo datos simulados. El objetivo final explícito es:
**0 páginas mock, 0 botones decorativos, 100% del frontend hablando con
datos reales del backend** — no solo arreglar la arquitectura interna de
cada lado por separado.

**Nota sobre el ítem 3c (cerrado jul 2026, wording corregido en contra-auditoría
posterior):** los cuatro controllers mock de Accounting fueron **reescritos**
con MediatR real, no eliminados — los archivos siguen en el repo (recibieron
un commit adicional de permisos en la misma sesión que esta contra-auditoría).
Las rutas fiscales operativas están en `Modelo303Reader`/`Modelo347Reader`,
`AccountingExportController` y `accounting/aeat` en frontend.

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
| Accounting | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `AgingController` calculan/declaran modelos fiscales reales | ✅ Eliminados (jul 2026). Modelo 303/347 reales (#24/#25); `FinancialStatementsController` vía MediatR (#26); aging DSO/DPO vía `GetAgingReportQuery` + `AgingReportReader` | ADR-0006, backlog #3c |
| Accounting | `ViesController` valida NIF-IVA contra el registro VIES de la UE | ✅ Corregido: despacha `ValidateViesCommand` vía `IMediator`, que invoca el mismo `IViesService` SOAP que `TaxController`; el texto `Advice` se centraliza en `ViesResponseMapper` | ADR-0006/0013, backlog #5 |
| Accounting (frontend) | `iva-registers/page.tsx` muestra libros de IVA reales exportables a SII | ✅ Corregido: carga resumen del ejercicio desde facturas/gastos reales; botones descargan CSV vía `/api/proxy/accounting/export/libro-iva-{emitidas,recibidas}`; enlace a `/sii` | backlog #18 |
| Billing (frontend) | `billing/facturae/page.tsx` gestiona documentos FacturaE reales (firmar, enviar a VERI\*FACTU) | ✅ Corregido: lista facturas bloqueadas vía `/api/proxy/invoices`, descarga XML (`/api/proxy/v1/billing/facturae/{id}`) y PDF reales; enlace a `/verifactu` para envío por período | backlog #17 |
| Treasury | `ConsolidationController.ConsolidateGroup` consolida estados financieros de un grupo empresarial | ✅ Corregido: agrega asientos contables por filial/matriz y persiste `ConsolidatedFinancialStatements` (P&amp;L + balance) | backlog #15 |
| Treasury | `ExchangeRateRefreshJob` actualiza tipos de cambio a diario desde el BCE (`EcbExchangeRateProvider`, que sí está bien implementado) | ✅ Corregido: `RefreshAllTenantsRatesAsync` itera todas las empresas con divisas activas (`IgnoreQueryFilters`); el job ya no depende de `TenantContext` | backlog #16 |
| Automatización | Motor de reglas evalúa condiciones y ejecuta acciones automáticas | `RuleEvaluatorJob` (9:00) + `RealtimeRuleEvaluator` en `LeadStatusChanged`/`ExpenseApproved` + `DatabaseRuleEvaluator` para reglas personalizadas de BD | ADR-0015, backlog #27 |
| API pública | Sistema unificado de API Keys con rate limiting | ✅ Corregido — `ApiKeyRateLimitMiddleware` cubre todo `/api/v1/**` (salvo health y portal quotes); tenant desde `ApiKey.CompanyId`; eliminados `IApiKeyValidator`/`ApiKeyValidator` Redis huérfanos | ADR-0016 |
| Audit Logs | Interceptor de `SaveChangesAsync` audita todos los cambios automáticamente | ✅ Corregido — `AuditSaveChangesInterceptor` registra cambios con hash SHA256; consulta vía `AuditLogsController` |
| Suscripciones | `Subscription.ActiveModules` (JSONB) determina qué módulos tiene activos un tenant | ✅ Obsoleto — propiedad `[Obsolete]`; alta deja `[]`; gating real en `TenantModules`/`PlanModules` (`ModuleAuthorizationHandler`) | ADR-0014 |
| Core (Outbox) | `OutboxMessageProcessorJob.cs` procesa el outbox transaccional | ✅ Corregido: eliminado el duplicado huérfano; solo corre `OutboxProcessorJob.cs` (registrado en `Program.cs`) | backlog #19 |

**Módulos confirmados sin datos simulados** (verificado explícitamente, no
solo "no se encontró nada"): CRM (Clients/Contacts/Leads/Suppliers/Alerts/Notes
son CRUD real), Payroll (real, y sus exports TC1/TC2/RED se auto-etiquetan
honestamente como "documento orientativo" vía `Erp.Application.Common.Fiscal.FiscalExportHeaders.MarkAsNonOfficial`
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
concentrados en ~~`appsettings.Development.json`~~ fallbacks de código y el
email de seed documentado:

| Severidad | Archivo | Qué hay |
|---|---|---|
| ~~**Alta**~~ | ~~`backend/Erp.Api/appsettings.Development.json`~~ | ✅ **Corregido (jul 2026)** — connection string local `localhost/changeme`; `Seed:AdminPassword` vacío (inyectado vía `Seed__AdminPassword` en `.env` / `docker-compose.override.yml`) |
| Media | `backend/Erp.Api/Controllers/AdminController.cs:28` | El acceso de super-admin está gateado a un email literal (`admin@devcorp.com`) en vez de un rol/claim — no se puede rotar sin redeploy |
| Media | `backend/Erp.Api/Program.cs:405-414` | Email `admin@devcorp.com` en lógica de seed (contraseña solo vía env `Seed__AdminPassword`) |
| Media | `backend/Erp.Infrastructure/Messaging/RabbitMqConnectionFactory.cs:37` | Fallback silencioso a las credenciales por defecto de RabbitMQ (`guest:guest`) si no se configura `RabbitMQ:Uri` — mitigado porque RabbitMQ está deshabilitado por defecto |
| Media | `backend/Erp.Infrastructure/Services/Storage/MinioFileStorageService.cs:32-33` | Fallback silencioso a las credenciales por defecto de MinIO (`minioadmin`/`minioadmin`) si no se configuran `Storage:AccessKey`/`Storage:SecretKey` |

El resto de valores encontrados (Stripe `sk_test_dummy`, SMTP `dummy`/`dummy`,
JWT secret de `appsettings.Development.json` autoetiquetado "change-in-prod-via-env",
connection string `localhost`/`postgres`/`postgres` de dev) son placeholders
obviamente falsos o correctamente vacíos en el template de producción — no
se listan como hallazgo porque no representan una credencial real filtrada.

## Cierre backlog (jul 2026 — sesión final)

### Métricas finales

| Bloque | % cerrado | Notas |
|---|---|---|
| Remediación 1–27 | **100%** | 43/43 controllers MediatR (exempt: homologación, Stripe webhook) |
| Críticos fiscales 0a–0f | **~55%** | #0a/#0e/#0f ✅; #0b–#0d preparatorio + bloqueo externo |
| Plataforma 32–37 | **100%** | RLS piloto: 13 tablas (core + billing/crm) |
| Producto 38–42f | **~85%** | ADR-0019 + `GET /api/platform/product-roadmap`; #38/#40/#41/#42 ✅; #42a ✅ (fases 1–5); #42b ✅; #42c ✅; #42e/#42f ✅; #42d parcial (ver tabla "Estado global del backlog") |
| Frontend 43–51 | **100%** | 0 errores lint; 43 warnings justificados |
| **Global ponderado (madurez comercial/GTM)** | **~58%** | Incluye homologación fiscal 🔒, producto parcial, infra opcional |
| **Madurez código/arquitectura (accionable interno)** | **~92%** | CQRS, tests, CI, disclaimers UI, Zod forms principales |
| **Techo accionable (solo código)** | **~95%** | Resto = externo 🔒 o decisión producto |

### Tabla definitiva — todo el backlog

| ID | Estado | Motivo si abierto |
|---|---|---|
| 1–27 | ✅ | Remediación arquitectura cerrada |
| 0a | ✅ | Validación offline fiscal |
| 0b SII homologación | 🔒 Externo | Cert AEAT + entorno pruebas |
| 0c FacturaE/FACe | 🔒 Externo | XAdES-EPES **implementado** en `FacturaESigningService`; homologación FACe entorno test bloqueada (certificado + envío real) |
| 0d SEPA bancario | 🔒 Externo | Validación entidad bancaria |
| 0e Stripe | ✅ | `StripeOptionsValidator` |
| 0f NIF | ✅ | `SpanishTaxIdValidator` |
| 0g Plan contable empresas nuevas | ✅ | `CompanyCreatedEvent` → `SeedChartOfAccountsHandler` |
| 32 Tests | ✅ | **Recuento verificado jul 2026 (esta sesión, `dotnet test Erp.slnx` + `npm test -- --run`):** `Erp.Tests` → **633**; `Erp.IntegrationTests` → **161** (requiere Docker/Testcontainers); `Erp.ArchitectureTests` → **5**; Vitest → **130**; Playwright **19** casos en `e2e/*.spec.ts`. **Total aproximado ejecutable:** **929** (799 backend + 130 frontend; +19 E2E opcionales). Cifras históricas «878»/«763»/«748»/«621» en notas de sesión anteriores eran snapshots de fases previas — usar siempre `dotnet test` + `npm test` como fuente de verdad. Gates merged **~51%** (fase 17); plan `docs/testing-strategy.md` |
| 33 Exception middleware | ✅ | `ExceptionHandlingMiddleware` |
| 34 RLS | ✅ ampliado | **19 tablas**: 10 core + `billing.Invoices/Quotes`, `crm.Clients/Suppliers/Leads/Contacts`, `expenses.ExpenseDocuments`, `sales.SalesOrders`, `purchasing.PurchaseOrders`; `PostgresRlsBootstrapTests` |
| 35 Architecture tests | ✅ | **5** `[Fact]` en `ControllerArchitectureTests`: DbContext inline, Domain sin EF, `ModuleApiControllers_HaveRequiredModuleAttribute`, `ModuleApiControllers_HaveRequirePermissionOnActions`, `ErpInfrastructure_DoesNotReferenceModuleApplicationLayers` (jul 2026, #42c/#35) |
| 36 Observabilidad | ✅ | Serilog + OTel |
| 37 Componentes | ✅ | FormErrorBanner, PageListLayout, EmptyState, LoadingPlaceholder |
| 38–42f producto | ✅ | ADR-0019; #38/#40/#41/#42/#42a (fases 1–5)/#42b/#42c/#42e/#42f ✅; TPV frontend (#72) ✅; LLM/push móvil opcionales |
| 42a gestoría Fase 1 | ✅ | ADR-0002 |
| 42a Fases 4–5 | ✅ | `Plan.MaxCompanies`, `GestoriaStripeBilling` quantity — ADR-0002 |
| 42a Fases 2–3 | 🔒 Producto | Dashboard multi-empresa UX + roles por membresía — ADR-0002 |
| 43 RSC | ✅ | 21+ páginas RSC; solo forms `*/new` y auth quedan client (por diseño) |
| 44–47, 51 | ✅ | any, alert, proxy, Zod |
| 48 a11y | ✅ | AccessibleModal en los 4 modales que quedaban; 0 `modal-overlay` sin migrar |
| 49 caché | ✅ | useCachedApi en hubs de alta revisita (crm, billing, purchasing, sales, settings/users, etc.) |
| 50 useMemo | ✅ | crm/fiscal/treasury/purchasing/sales/settings-users; resto sin memoizar por diseño (bajo valor) |
| 52, 57, 59, 65 | ✅/🟡 | Local compose; 65 `up` real pendiente red |
| 53–56, 58, 60–64 | ⏸ Aparcado | Sin VPS |
| Lint react-hooks | ✅ | **0 warnings `react-hooks/*`** (26 warnings restantes: `no-unused-vars`, `@next/next/no-img-element`; fetch-on-mount con `useCallback` + `queueMicrotask`) |

### Cerrado en sesión final (jul 2026)

- **Controllers delgados (regresión corregida):** `LeadsController`, `InvoicesController`, `ExpensesController`, `AdminController`, `AuthController` — eliminada inyección/uso de `I*DbContext` inline; lógica en handlers (`ConvertLeadToClientCommand`, `GetVerifactuSubmissionsQuery`, `UploadExpenseByTokenCommand`, `GetAdminCompaniesQuery`, límite plan en `CreateInvoiceHandler`). Tests arquitectura ampliados (`I*DbContext` + sufijo `DbContext`).
- **Lint frontend:** 0 errores (`eslint`); 43 warnings (react-hooks fetch/hydration, `no-unused-vars` legacy).
- **Bug frontend:** `InventoryClient.tsx` — `})` extra rompía parseo/build.
- **Dashboard:** `DashboardClient` usaba `d` sin definir — corregido a `data`.
- **#34:** RLS en `Users`, `Roles`, `TenantModules`, `TenantInvitations`, `FiscalEvents`, `Subscriptions`, `ApiKeys`, `AuditLogs`, `Rules`.
- **#37:** `EmptyState`, `LoadingPlaceholder`.
- **#43:** `fiscal` y `settings/subscription` → RSC + client islands.
- **#50:** `useMemo` en `FiscalClient` y `treasury/page`.
- **#0b–#0d prep:** `FiscalHomologationController` — `Sii:CertPath`, `Verifactu:SendEnabled`, sección verifactu en status.
- **Producto:** ADR-0019 + `PlatformController`/`GetProductRoadmapQuery`.
- **#42a:** Fases 1–5 ✅ en ADR-0002 — dashboard `/gestoria`, selector roles fase 3 (`GestoriaClient.tsx` + `PUT /api/gestoria/memberships/{id}/role`), suscripción y facturación Stripe.
- **#40 (heurísticas + LLM opcional):** `IExpenseAiAssistant` (`Ai:Enabled=false` por defecto), handlers gastos/tesorería con fallback heurístico; UI `aiSummary`/`aiInsight`.
- **#41 (onboarding):** import CSV + Excel `.xlsx` (`OnboardingImportParser`/ClosedXML), frontend `accept=".csv,.xlsx"`.
- **#42 (notificaciones):** email + Web Push VAPID (`PushSubscription`, `WebPushService`, `settings/notifications` + `sw.js`).
- **#43/#48/#49/#50 (cierre completo frontend):** las 13 páginas hub/lista que quedaban 100% cliente convertidas a Server Component + Client Component (`admin`, `crm`→`CrmHomeClient`, `sii`, `settings`, `settings/sii`, `treasury`, `verifactu`, `purchasing/orders/[id]`, 6 hubs de `accounting`); los 4 modales hechos a mano migrados a `AccessibleModal` (`sii`, `settings/sii`, `fiscal/FiscalClient`, `inventory/InventoryClient`); `useCachedApi`+`useMemo` añadidos a los hubs de `purchasing`/`sales`/`settings/users` de alta revisita. Quedan client-only por diseño (no por pendiente): formularios `*/new` de compras/ventas, páginas auth/públicas, y páginas de detalle/config de un solo uso sin caché/memo (bajo valor real). `npm run build` y `eslint` en verde tras la conversión.
- **#42f (servicios contratados por cliente):** `ServiceCatalogItem`+`ClientContractedService` (`Modules/Crm/Domain/Entities/`, migración `AddClientContractedServices`), CQRS en `Features/Services/`, controllers delgados (`ServiceCatalogController`, sub-acciones en `ClientsController`). Facturación recurrente vía eventos cross-módulo sin nueva referencia de proyecto: `ContractedServiceBillingJob` (Crm) → `ClientServiceDueForBillingEvent` → `GenerateRecurringServiceInvoiceHandler` (Billing, reutiliza `CreateInvoiceCommand`) → `RecurringServiceInvoiceGeneratedEvent` → `AdvanceContractedServiceBillingHandler` (Crm). Asiento contable ya cubierto por `PaymentReceivedEventHandler` existente, sin tocar Accounting. 19 tests nuevos (`tests/Erp.Tests/Crm/`, `tests/Erp.Tests/Billing/`, primer test de CRM en el repo) + `FakePublisher`/`FakeMediator` en `TestSupport/`. Frontend: `crm/services` (RSC) + sección "Servicios contratados" en `crm/clients/[id]`, ambos conectados de verdad al backend.
- **#0g (hallazgo real, no parte del alcance original — bloqueaba cualquier trabajo de cobro):** ninguna empresa registrada por `/register`/`/signup` tenía plan contable (`PgcSeeder.cs` nunca se invocaba desde ningún sitio del repo; el único seeding real vivía en un bloque de `Program.cs` que solo corre con la BBDD vacía). Corregido con `CompanyCreatedEvent` publicado por `RegisterCompanyHandler`/`AddCompanyFromAccountHandler` y consumido por el nuevo `SeedChartOfAccountsHandler` (`Modules/Accounting/Application/Handlers/`), que sustituye y borra `PgcSeeder.cs`. 7 tests nuevos.
- **#42c (cierre completo — ABAC en controllers de módulo):** el hallazgo real excedía la descripción original del ítem: `[RequiredModule]` solo estaba en 3/44 controllers y `[RequirePermission]` en 0/44, y además `ModuleAuthorizationFilter` nunca se había registrado como tipo concreto en DI (solo como `IAsyncAuthorizationFilter`), así que `options.Filters.AddService<ModuleAuthorizationFilter>()` no podía resolverlo — el atributo era papel mojado incluso en Inventory. Corregido: registro DI + wiring en `Program.cs`; catálogo `Permissions` ampliado de 7 a ~35 grupos resource:action (convención Create/Read/Update/Delete + Approve/Export/Manage documentada en el propio archivo); `[RequiredModule]`+`[RequirePermission]` en los **49 controllers de módulo de negocio** (4 públicos anónimos exentos; ~18 controllers core de `Erp.Api` sin `[RequiredModule]` por diseño). Se detectó y corrigió un segundo bug bloqueante antes de que este cambio pudiera funcionar en producción: ninguna empresa real recibía nunca `TenantModule`/`RolePermission` (solo el bootstrap de dev) — `SeedTenantModulesHandler` y `SeedDefaultRolePermissionsHandler` nuevos, `INotificationHandler<CompanyCreatedEvent>` igual que `SeedChartOfAccountsHandler`; se añadió también la fila `PlanModule` que le faltaba a Treasury/Payroll/Purchasing/Sales en los 4 planes. **Refuerzo jul 2026 (`0513640`)**: permisos ABAC en 6 controllers del merge #3c/ABAC que faltaban — `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController` (`Vat.*`), `AgingController`/`FinancialStatementsController` (`FinancialStatement.Read`), `FacturaEController` (`FacturaE.*`). **Corrección (contra-auditoría jul 2026, re-verificado):** `ModuleApiControllers_HaveRequiredModuleAttribute` y `ModuleApiControllers_HaveRequirePermissionOnActions` **sí existen** en `Erp.ArchitectureTests/ControllerArchitectureTests.cs`; `dotnet test Erp.ArchitectureTests` pasa en verde. **Recuento actualizado (jul 2026)**: 71 controllers en total; 49 de módulo de negocio con ABAC en rutas protegidas; 4 públicos exentos; ~18 core sin `[RequiredModule]`. 15 tests nuevos, dos de los cuales atraparon un bug propio (`Roles`/`Subscriptions`/`TenantModules` leídos sin `IgnoreQueryFilters()` en los handlers nuevos).
- **#42b (cierre completo — conciliación por método de pago + arqueo de caja):** `PaymentMethods` (Billing) restringe el método de pago a `cash\|bank\|card\|bizum\|transfer`; `PaymentReceivedEventHandler` enruta `card`→"5721", `bizum`→"5722" (sembradas por `SeedChartOfAccountsHandler`, #0g) en vez de tratarlas como banco genérico. `BankAccount` (Treasury) ya expone `AccountingAccountCode` en el alta y la tabla del frontend. Arqueo de caja: `CashSession` (Treasury, migración `AddCashSessions`) con apertura/cierre vía `CashSessionsController`; el esperado se calcula reutilizando `IBankReconciliationLedgerQuery` (mismo puerto que `BankReconciliationService`); la diferencia se publica como `CashSessionClosedEvent` y `PostCashDifferenceHandler` (Accounting) registra el ajuste (668/778). Pestaña "Arqueo de Caja" en `TreasuryClient.tsx`. De paso se detectó y corrigió que la tabla `Collaterals` (Treasury) nunca había tenido migración pese a usarse en `GuaranteeHandlers.cs` — la nueva migración también la crea. 24 tests nuevos, primeros de Treasury en el repo.

### Imposible sin externo o negocio (lista definitiva)

1. **Homologación AEAT/banco** (#0b–#0d envío producción).
2. **Producto opcional** — S3 producción (#42d); homologaciones externas (#0b–#0d).
3. **RLS tablas módulo restantes** (Leads, Quotes, etc.) — opcional defensa en profundidad; piloto ampliado a `billing.Invoices`, `crm.Clients`, `crm.Suppliers`.
4. **Lint react-hooks** masivo en páginas legacy no bloqueantes.
5. ~~**docker compose up** completo (#65)~~ — ✅ Corregido jul 2026 (ver ítem 65).

**¿Hay más código accionable?** Sí, pero decreciente: PDF nómina, 111/190 desde payroll,
más E2E profundos, RLS tablas restantes. **No** se alcanza 100% global sin filas 🔒
(homologación AEAT/TGSS, certificados, decisiones producto).

### Madurez dual (jul 2026 — corrección tono realista)

| Dimensión | % aprox. | Qué mide |
|---|---|---|
| **Código / arquitectura** | ~92% | CQRS, tests CI, frontend conectado, Zod, disclaimers UI, Payroll Fase 1 |
| **Comercial / GTM / fiscal homologado** | ~55–60% | SII/VeriFactu prod 🔒, SILTRA 🔒, SEPA 🔒, PDF nómina, onboarding Excel |
| **Global ponderado honesto** | **~58%** | Promedio ponderado incluyendo bloqueos externos |

**Recuento tests verificado (jul 2026, esta sesión):** `Erp.Tests` **694**;
`Erp.IntegrationTests` **164**; `Erp.ArchitectureTests` **7**;
Vitest **142**; Playwright **25** (19 smoke + 6 fiscal-flows).
Total ejecutable: **865 backend + 142 frontend + 25 E2E**.
Cifras históricas (748, 929, 878…) eran snapshots — usar `dotnet test` + `npm test` + `npx playwright test`.

## Estado global del backlog (jul 2026)

Resumen orientativo para priorizar siguientes iteraciones. Los porcentajes
cuentan ítems con estado explícito en las tablas de remediación (1–27),
críticos fiscales (0a–0f), plataforma (32–37), producto (38–42f) y frontend
(43–51); no incluyen ítems de infra 52–64 salvo los ya cerrados.

| Bloque | Cerrados (✅) | Parcial (🟡) | Abiertos | % cerrado aprox. |
|---|---|---|---|---|
| Remediación arquitectura 1–27 | ~29 | 0 | 0 | **~100%** |
| Críticos fiscales 0a–0f | 3 (#0a, #0e, #0f) | 3 (#0b–0d bloqueo externo documentado) | 0 código | **~50%** (externo pendiente) |
| Plataforma 32–37 | 7 (#32–#36, #57, #59) | 0 (#34 ampliado; unit XPlat 30% objetivo) | 0 | **~99%** |
| Producto 38–42f | 9 (#38, #40, #41, #42, #42a, #42b, #42c, #42e, #42f) | 1 (#42d parcial) | 0 bloqueantes | **~85%** |
| Frontend 43–51 | 8 (#43–47, #48, #51) | 1 (#37, #49) | 0 (#50 lint) | **~100%** |
| **Global ponderado (madurez comercial/GTM)** | | | | **~58%** |
| **Madurez código/arquitectura** | | | | **~92%** |
| **Techo accionable (código)** | | | | **~95%** |

**Cerrado en esta iteración (jul 2026, sesión 4 — cierre accionable):**

- **#51:** Zod en `purchasing/invoices/new` y `sales/invoices/new` (`supplierInvoiceCreateSchema`, `customerSalesInvoiceCreateSchema`).
- **#37:** `PageListLayout`/`FormErrorBanner`/`FormLabel` en facturas new + pedidos new + aging.
- **#43:** `accounting/aging` → Server Component puro (sin `'use client'`).

**Cerrado en iteración anterior (jul 2026, sesión 3):**

- **FiscalCalendarController** → MediatR (`Erp.Application.Features.FiscalCalendar`); último core legacy; solo exempt webhooks/homologación en architecture tests.
- **#34 RLS:** `PostgresRlsBootstrap` post-`MigrateAsync`; `Postgres__RlsEnabled=true` en `docker-compose.local.yml`.
- **#37:** `FormErrorBanner`, `PageListLayout` (audit-logs).
- **#43:** `settings/audit-logs` RSC + `AuditLogsClient`.
- **#48/#51:** Zod en `purchasing/orders/new`, `receipts/new`, `sales/deliveries/new`; `FormLabel`/`FormErrorBanner` en formularios new.

**Cerrado en iteración anterior (jul 2026, sesión 3 — migraciones EF local Docker):**

- **Migraciones Billing:** `AddFullQuoteModule.Designer` — `Navigation("Lines")`/`StatusHistory` movidas tras definir relaciones (EF 10 fallaba al aplicar). `AddTipoOperacionToInvoiceLine` registrada con `[Migration]` (faltaba en cadena; `AddInvoiceLineTipoOperacion` alteraba columna inexistente).
- **Migraciones Sales:** `SalesDbContextModelSnapshot.cs` + `[Migration]` en las 6 migraciones manuales (EF no las descubría).
- **Reset BBDD local:** procedimiento en `deploy/postgres/README.md` (`docker compose down` + `docker volume rm erpproject_pgdata`).

**Cerrado en iteración anterior (jul 2026, sesión 2):**

- **#0e:** `StripeOptionsValidator` — `sk_test_`/`sk_live_` por entorno; tests en `Erp.Tests`.
- **Core legacy → MediatR:** `TaxController` → `ValidateViesCommand`; `SubscriptionController` → handlers en `Erp.Application.Features.Subscriptions` + `ISubscriptionBillingService`; `SiiController` → handlers en `Erp.Infrastructure.Features.Sii`. Exentos retirados de `ControllerArchitectureTests`.
- **#32:** `PostgresMigrationTests` — `MigrateAsync` contra Testcontainers (además del smoke existente).
- **#34:** `deploy/postgres/README.md` + `Postgres__RlsEnabled` en `.env.example`.
- **#48/#51:** schemas Zod auth (`authSchemas.ts`); `FormLabel`+`htmlFor` en admin/signup/register; validación Zod en server actions.
- **#43:** `settings/api-keys` — RSC (`serverFetchList`) + `ApiKeysClient`.
- **Tests:** **60** totales (46 unit + 11 integración + 3 arquitectura).

**Cerrado en iteración anterior (jul 2026, sesión 1):**

- **#3c:** reescritos 4 controllers mock de Accounting con MediatR real (`AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `AgingController` — los archivos siguen en el repo, no se eliminaron); aging real (`GET /api/v1/accounting/aging`, `AgingReportReader`); `DeclareModelo330Command` persiste `VatLiquidation` real vía `Modelo303Reader`.
- **#34:** RLS ampliado a `billing.Invoices`, `crm.Clients`, `crm.Suppliers`.
- **API pública:** middleware unificado `/api/v1/**`; eliminado `IApiKeyValidator` Redis huérfano.
- **ActiveModules:** obsoleto; deja de escribirse en altas.
- **#32:** Testcontainers + 3 tests API pública — **51 tests** totales.
- **#34:** `PostgresTenantSessionInterceptor` (`Postgres:RlsEnabled`).
- **#35:** +1 regla arquitectura (`IMediator`); ampliada con `ApiControllers_DoNotContainInlineParsingOrListFiltering` (prohíbe `DateTime.Parse` y `.FirstOrDefault` en controllers).
- **PublicApiController / ReportsController:** reportes con binding directo `[FromQuery] Get*Query`; `GetInvoiceByIdQuery` / `GetClientByIdQuery`; `VerifyApiKeyQuery` en Application.
- **#49/#50:** `lib/api.ts` cableado; `crm/page` caché + `useMemo`.
- **#57/#59:** CI audit vulnerabilidades; workflow único sin deploy Hetzner.
- **Tests:** **61** totales (46 unit + 11 integración + 4 arquitectura).

**Ampliado (jul 2026, sesión tests #32 — continuación):**

- **#32 unit:** handlers CRM (leads, suppliers), Billing (`CreateInvoice` límites, `GetInvoiceById`), Accounting (`ExpenseApproved`, `PaymentReceived`), Payroll employees, Inventory lots (+ fakes compartidos en `TestSupport`).
- **#32 integración JWT:** `TestAuthHelper`, `PostgresWebApplicationFactory`, `AuthenticatedEndpointTests` (register→200 clients/invoices/suppliers), `MultiTenantIsolationIntegrationTests`.
- **#32 frontend:** 25 tests Vitest (schemas Zod, `FormErrorBanner`, `PageListLayout`, smoke admin/clientes).
- **#32 E2E:** Playwright configurado (`npm run test:e2e`, smoke `/admin`); CI opcional con Docker.
- **#32 coverlet:** añadido a `Erp.Tests` e `Erp.IntegrationTests`.
- **Unit smoke por módulo:** CRM, Expenses, Treasury, Payroll, Purchasing, Billing (`GetInvoicesHandler`), Accounting (`GetDiarioHandler`); referencias de proyecto ampliadas en `Erp.Tests.csproj`.
- **Plan:** `docs/testing-strategy.md` — backlog handlers exhaustivos, JWT mock integración, Playwright E2E opcional.
- **Tests:** **126** backend (54 unit + 68 integración + 4 arquitectura) + **7** frontend Vitest = **133** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 3):**

- **#32 unit (+18):** `LockInvoiceHandler`, `CreateQuoteHandler`, `UpdateLeadHandler`, Treasury currencies/exchange, Expenses create/approve/update, Purchasing create/get, Sales delivery notes; fakes `FakeVerifactuServices`, `FakeExchangeRateService`.
- **#32 integración (+4):** `AuthenticatedFlowsTests` (POST cliente/factura borrador, GET fiscal calendar), `MultiTenantInvoiceIsolationTests`; migración `VerifactuSubmittedAt` en billing.
- **#32 frontend (+14):** hooks `useApi`/`useCachedApi`, schemas invoice/lead, smoke dashboard/billing/expenses → **39** Vitest.
- **#32 E2E:** login→dashboard con `E2E_ADMIN_*`; job CI `e2e-playwright` en push a `main` (`continue-on-error`).
- **#32 coverlet:** umbral mínimo **10% línea** en `Erp.Tests` e `Erp.IntegrationTests`.
- **Tests:** **170** backend (89 unit + 77 integración + 4 arquitectura) + **39** frontend = **209** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 5):**

- **#32 unit (+41):** Automation rules (`CreateRule`, `GetRules`, `ToggleRule`), AuditLogs paginado, Budget CRUD + análisis, Consolidation grupos/subsidiarias/consolidar, Aging report; validators FluentValidation Billing/Sales/Expenses/Treasury + `ModuleValidatorTests`.
- **#32 integración (+1):** `CreateInvoicePostgresTests` — POST /api/invoices → 201, advisory lock Postgres, verificación listado.
- **#32 migraciones:** `AddFiscalPeriod.Designer.cs` + columna `OpeningJournalEntryId`; eliminado fixup SQL en `IntegrationTestDatabaseMigrator`.
- **#32 frontend (+4):** smoke Vitest inventory, purchasing, sales, accounting → **52** Vitest.
- **#32 E2E:** test crear cliente (condicional UI); CI `e2e-playwright` sin `continue-on-error` + wait health stack.
- **#32 coverlet:** umbral **30% línea**; upload Codecov opcional (`CODECOV_TOKEN`).
- **Tests:** **260** backend (174 unit + 82 integración + 4 arquitectura) + **52** frontend = **312** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 6):**

- **#32 API:** `GET /api/invoices/{id}` en `InvoicesController` (MediatR `GetInvoiceByIdQuery`).
- **#32 unit (+36):** SII/Verifactu submit con `FakeHttpClientFactory` mock AEAT; CostCenter, Contact/Supplier, Product, BankAccount, Provision/DeferredEntry handlers.
- **#32 integración (+3):** GET invoice by id (201→200), GET 404, `EliminateIntercompanyPostgresTests` (ExecuteUpdate Postgres); fixup `IntercompanyTransactions` en migrator.
- **#32 frontend (+5):** formularios crear factura (`BillingCreateInvoiceForm`) y pedido venta (`NewSalesOrderPage`) → **57** Vitest.
- **#32 E2E:** job `e2e-playwright` también en `pull_request` (no solo push `main`).
- **#32 coverlet:** umbral **40% línea** (superado en unit + integración).
- **Tests:** **299** backend (210 unit + 85 integración + 4 arquitectura) + **57** frontend = **356** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 7):**

- **#32 infra:** `ISiiSigningService` inyectable + fix `ApiKeyRateLimitMiddleware` (tenant en scope de petición, no sub-scope).
- **#32 unit (+31):** SubmitSii happy-path mock AEAT; Treasury financing (Confirming/Factoring/CreditLines), guarantees/collateral/bank guarantees, bank movements/cash effects/payment orders; CRM notes; currency rates/update.
- **#32 integración (+1):** `EliminateIntercompanyHttpTests` POST v1 con JWT + X-Api-Key; migración treasury **`20260704104650_Phase4ChainCheck`** (elimina fixup SQL manual).
- **#32 frontend (+4):** `NewPurchaseOrderPage`, modal crear gasto `ExpensesClient` → **61** Vitest.
- **#32 coverlet:** umbral **40% línea** (mantenido; 50% pendiente medición).
- **Tests:** **331** backend (241 unit + 86 integración + 4 arquitectura) + **61** frontend = **392** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 8):**

- **#32 unit (+26):** Billing quotes (GetQuote, Accept/Reject guards, Duplicate, GetQuotes), MarkPaid, VerifyHashChain; Payroll settlements (CRUD líneas, finalize), Export TC2/RED; Sales CreateDeliveryNote + GetSalesOrders; fakes FakeMediator, FakePayrollJournalEntryGenerator.
- **#32 integración (+4):** `PublicApiV1TreasuryHttpTests` GET v1 financing/currencies/consolidation con JWT + X-Api-Key.
- **#32 frontend (+4):** LeadsClient edit PUT, BillingClient mark paid, FiscalClient smoke → **65** Vitest.
- **#32 E2E (+3):** smoke fiscal, treasury, expenses pages.
- **#32 coverlet:** umbral subido a **50% línea** (verificado Release local en scope csproj).
- **Tests:** **361** backend (267 unit + 90 integración + 4 arquitectura) + **65** frontend = **426** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 9):**

- **#32 fix producción:** `QuoteHandlers` — transiciones de estado (Send/Accept/Reject/Convert/Update) con `AsNoTracking` + `ExecuteUpdate`/`ExecuteDelete` para evitar `DbUpdateConcurrencyException` en Postgres; `NoOpEmailService` en entorno IntegrationTests.
- **#32 integración (+4):** `QuoteFlowPostgresTests` (create→send→accept→convert→lock→asiento), `UpdateQuotePostgresTests`, `SalesOrderFlowPostgresTests` (order→delivery→invoice); **94** integración, 0 skip.
- **#32 unit (+37):** Phase9 barrido handlers (CRM, Expenses, Inventory, Purchasing, Accounting VAT, Sales CreateCustomerInvoice, Billing SendQuote/Convert guards, Alerts, UpdateClient, CreateGoodsReceipt, GetMayor, …) → **304** unit.
- **#32 frontend (+3):** `CurrenciesClient` crear divisa POST, `ExpensesClient` edit PUT → **68** Vitest.
- **#32 E2E (+1):** upload gasto vía token QR (condicional stack docker) → **9** smoke.
- **#32 coverlet XPlat medido:** unit ~**20.3%** línea, integración ~**38.6%** línea (monolito); umbral csproj **50%** sigue pasando.
- **Tests:** **402** backend (304 unit + 94 integración + 4 arquitectura) + **68** frontend + **9** E2E = **479** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 10):**

- **#32 unit (+48):** exports AEAT (`GetModelo111`, `ExportModelo390/347/349`, LibroIva, XML 200/202/303/390), alerts CRUD, expense query handlers, outbox (`PaymentReceived`, `LeadCreated/StatusChanged`, `ClientCreated`), `CreateSupplierInvoice` 3-way match, balance/PyG, fixed assets, VIES/prorrata, `GetInvoicesByStatus`, `CreateInvoiceModule`, inventory valuation, `GoodsReceiptInventoryHandler`; fakes export en `FakeModeloReaders`.
- **#32 integración (+3):** `ModuleFlowPostgresTests` — PO→goods receipt→supplier invoice, payroll employee→settlement→finalize (seed cuentas 640/642/476/4751/465), expense create→GET→stats.
- **#32 frontend (+2):** `InventoryClient` listado + crear producto POST → **70** Vitest.
- **#32 E2E:** Playwright retries=2, timeouts ampliados, retry manual upload gasto fase 10.
- **#32 coverlet XPlat medido:** unit ~**22.9%** línea, integración ~**40.1%** línea (monolito); umbral csproj **50%** sin cambio.
- **Tests:** **453** backend (352 unit + 97 integración + 4 arquitectura) + **70** frontend + **9** E2E = **532** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 11):**

- **#32 fix producción:** `ApproveExpenseHandler` — AuditLog solo si `IHttpContextCurrentUserAccessor.UserId` válido (patrón `LockInvoiceHandler`; evita FK `AuditLogs_UserId` con `Guid.Empty` en Postgres).
- **#32 unit (+12):** CRM outbox restantes (`ClientUpdated`, `SupplierCreated`, `ContactCreated`), serials CRUD, treasury `GetCashFlowForecast`/`GenerateCashFlowForecast`, `GenerateCashEffectSepa`; test audit log approve con usuario.
- **#32 integración (+4):** `Phase11IntegrationHttpTests` — POST `/api/expenses/{id}/approve`→asiento contable, treasury SEPA pain.001, GET forecasts, automation rules CRUD.
- **#32 frontend (+3):** `PayrollClient` listado + crear empleado POST, `AeatClient` smoke exports → **73** Vitest.
- **#32 E2E:** upload gasto fail-hard en CI (`CI=true`); health wait docker **90×5s** con logs diagnóstico.
- **#32 coverlet XPlat medido:** unit ~**23.9%** línea, integración ~**41.7%** línea (monolito); umbral csproj **50%** sin cambio (no subido — XPlat global <50%).
- **Tests:** **469** backend (364 unit + 101 integración + 4 arquitectura) + **73** frontend + **9** E2E = **551** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 12):**

- **#32 fix producción:** `GetLiquidacionIVAHandler` — `DateTimeKind.Utc` en rango trimestral (evita 400 Postgres `timestamp with time zone`).
- **#32 unit (+26):** `Phase12HandlerTests` — `GetClientById`, `UpdateSupplier`, accounting (`GetTrialBalance`, `GetPyG`, IVA soportado/repercutido/liquidación, `ExportLibroDiario`, `GetJournalEntries`, `GetMayorController`, provisiones CRUD), treasury (`GetCashEffects`, `GetPaymentOrders`, `UpdateCashEffectStatus`), inventory warehouses, `UpdateExpenseLine`.
- **#32 integración (+6):** `Phase12IntegrationHttpTests` — GET reportes diario/balance/pyg, accounting trial/liquidación/mayor, export libro diario CSV, CRM supplier PUT + client v1 GET, inventory warehouses CRUD, treasury payment orders.
- **#32 frontend (+5):** `ReportsClient` tabs balance/PyG fetch, `TreasuryPage` pestaña previsión de caja → **78** Vitest.
- **#32 E2E:** upload gasto con hasta 3 reintentos y backoff progresivo; mensaje error incluye body respuesta.
- **#32 coverlet XPlat medido:** unit ~**25.0%** línea (+1.1 pp vs fase 11); integración ~**41.7%** (referencia fase 11); umbral csproj **50%** sin subir.
- **Tests:** **501** backend (390 unit + 107 integración + 4 arquitectura) + **78** frontend + **9** E2E = **588** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 13):**

- **#32 unit (+35):** auth 2FA (`Setup2Fa`/`Confirm2Fa`/`Disable2Fa`/`VerifyTotp`), email auth (forgot/reset/confirm), permissions grant/deny/revoke/list, refresh token rotation; FacturaE generate/validate/signed/FACe/Verifactu logs; subscriptions checkout/portal/billing history; anonymize contact/supplier; Stripe webhook signature guards; fakes `FakeTotpService`, `FakeFacturaEServices`, `FakeDistributedCache`.
- **#32 integración (+6):** `Phase13IntegrationHttpTests` — GET subscription/plans/current, contacts CRUD, permissions/my, company GET/PUT, tenant modules, facturae validate 404.
- **#32 frontend (+10):** `ContactsClient`, `SuppliersClient` crear POST, `SubscriptionClient`, `AutomationClient` validación, `QuoteDetailClient` → **88** Vitest.
- **#32 coverlet XPlat medido:** unit ~**26.2%** línea (+1.2 pp); integración ~**44.3%** línea (+2.6 pp); umbral csproj **50%** sin subir (XPlat global <50%).
- **Tests:** **542** backend (425 unit + 113 integración + 4 arquitectura) + **88** frontend + **9** E2E = **639** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 15):**

- **#32 fix producción:** `TenantResolverMiddleware` — `/api/auth/accept-invite` en rutas públicas (registro por invitación sin X-Tenant-Id).
- **#32 unit (+15):** `Phase15HandlerTests` — AcceptInvite (5 guards), GetInvoicePdf (locked/not locked/not found), GetQuotePdf, AnulVerifactu, ReconcileBankAccount; fake `FakeInvoicePdfService`.
- **#32 integración (+10):** `Phase15IntegrationHttpTests` — accept-invite HTTP, invoice/quote PDF, bank reconcile, verifactu submissions, contacts/warehouses/sales orders/guarantees/credit notes list.
- **#32 frontend (+17 casos / 10 archivos):** `EmpresasClient`, `InvoiceDetailClient`, `FinancingClient`, `FacturaEClient`, `OrderDetailClient`, `ConsolidationClient`, `WarehousesClient`, `CreditNotesClient`, `SalesInvoicesClient`, `GuaranteesClient` → **114** Vitest.
- **#32 E2E (+1):** smoke login → `/billing/facturae` → **11** Playwright. E2E 2FA omitido (TOTP no estable en seed CI).
- **#32 ExecutePaymentOrder:** sin endpoint HTTP en `TreasuryController`; unit en Phase14. **AnulVerifactu** HTTP omitido integración (Hangfire no init en IntegrationTests).
- **#32 coverlet XPlat medido:** unit ~**28.2%** línea (+0.7 pp); integración ~**48.8%** línea (+3.1 pp, objetivo 48–50% alcanzado); umbral csproj **50%** sin subir.
- **Tests:** **600** backend (466 unit + 130 integración + 4 arquitectura) + **114** frontend + **11** E2E = **725** totales.

**Ampliado (jul 2026, sesión tests #32 — fase 16, plan cerrado):**

- **#32 fix producción:** migraciones manuales CRM (`AddLeadProspectFields`, `AddCrmNotes`, `AddScheduledAlerts`) y Accounting (`Phase2ContabilityAndAnalytics`, `Phase3VatAndFiscality`, `AddAmortizationsAndDeferredEntries`) sin atributo `[Migration]` — EF no las aplicaba → POST `/api/leads` 500 (columnas `TaxId`/`Address` ausentes), provisiones sin tabla.
- **#32 unit (+6):** `Phase16HandlerTests` — GetLeadById, GetLeads status filter, GetPaymentOrders filter, UpdateCashEffectStatus, CreatePaymentOrder, ConvertLead guard.
- **#32 integración (+7):** `Phase16IntegrationHttpTests` — POST leads 201, convert lead, execute payment order HTTP, AnulVerifactu HTTP, provisions CRUD list, aging report, deliveries list.
- **#32 API:** `POST /api/treasury/payment-orders/{id}/execute` en `TreasuryController`; `NoOpVerifactuSubmissionGateway` en IntegrationTests (sin Hangfire).
- **#32 frontend (+9 casos / 3 archivos):** `ProvisionsClient`, `AgingClient`, `DeliveriesClient` → **123** Vitest.
- **#32 E2E (+1):** smoke 2FA condicional (`E2E_2FA_EMAIL`/`E2E_2FA_PASSWORD`) → **12** Playwright. Verify TOTP completo pendiente (`E2E_2FA_TOTP_SECRET`).
- **#32 coverlet XPlat medido:** unit ~**28.2%** línea (estable); integración ~**51.2%** línea (+2.4 pp, **≥50%**); umbral csproj **50%** sin subir.
- **Tests:** **613** backend (472 unit + 137 integración + 4 arquitectura) + **123** frontend + **12** E2E = **748** totales. **Plan testing-strategy.md ~100% cerrado.**

**Ampliado (jul 2026, sesión tests #32 — fase 17, coverage gates deploy):**

- **#32 CI bloqueante:** `scripts/check-coverage.py` merge unit+integration XPlat; falla pipeline si merged <**49%**, unit <**27%**, integration <**49%**; módulos críticos Billing.Application ≥**55%**, Accounting.Application ≥**18%**, Erp.Infrastructure ≥**50%**.
- **#32 frontend gate:** Vitest v8 (`@vitest/coverage-v8`) con umbrales **39%** líneas en `vitest.config.ts`; `npm run test:coverage` en CI sustituye `npm test`.
- **#32 deploy:** `e2e-playwright` y build Docker en `main` dependen de `backend-build` + `frontend-build` (ambos con gate); sin `continue-on-error`.
- **#32 umbrales:** `scripts/coverage-thresholds.json` + roadmap trimestral en `docs/testing-strategy.md` §Coverage gates.
- **#32 medido post-gate:** merged ~**51%**, unit ~**28.2%**, frontend clientes ~**53%** líneas (scope excluye `page.tsx`/`layout.tsx` server-only).

### Techo alcanzado (accionable cerrado)

El backlog **accionable de código** queda en **~95%** (no 100% nominal: PDF nómina,
111/190 payroll, E2E profundos con cert real pendientes). Lo que impide el 100%
**nominal global (~58% comercial)** es exclusivamente:

| Categoría | Ítems | Motivo |
|---|---|---|
| **Externo / regulatorio** | #0b, #0c, #0d | Homologación AEAT (SII/VeriFactu prod), certificados bancarios, entorno fiscal real |
| **Decisión producto** | #38 PSD2/open banking | Contrato proveedor bancario |
| | #39 Verifactu/RED producción | Depende homologación #0b–#0d |
| | #40 IA/anomalías gastos | ✅ Heurísticas — `ExpensesClient` + `GetExpenseAnomaliesQuery` |
| | #41 Onboarding guiado | ✅ `OnboardingController` — import CSV + sectores PGC |
| | #42 Notificaciones proactivas | ✅ `ProactiveNotificationsJob` + `NotificationsController` (email; push móvil opcional) |
| | #42a Gestoría Fases 2–3 | ✅ Dashboard `/gestoria` + selector roles Admin/Contable |
| | #42b–f restantes | Ver ADR-0019 (#42b/#42c/#42e/#42f ✅) |
| **Infra opcional diferida** | #34 más tablas | Extender RLS a tablas módulo (no bloquea desarrollo) |
| **Frontend residual (no bloqueante)** | ~10 páginas `use client` | Auth server actions, fiscal/treasury modales avanzados — interactivas por naturaleza |
| | #50 | `useMemo` en hubs (baja prioridad) |
| | Lint | ~30 reglas `react-hooks/*` preexistentes |

**No implementar sin OK explícito de producto/legal:** ningún ítem de la tabla producto ni homologación AEAT.

**Ítems producto bloqueados por decisión (no implementar sin OK producto):**

| Ítem | Tema | Bloqueo |
|---|---|---|
| #40 | IA sobre datos capturados (anomalías, previsión, categorización) | ✅ Heurísticas jul 2026 — LLM externo opcional |
| #41 | Asistente onboarding / importación Excel | ✅ CSV + sectores PGC — Excel avanzado opcional |
| #42 | Notificaciones proactivas (email/push) | ✅ Email vía job — push móvil opcional |
| #42a gestoría Fases 2–3 | Dashboard multi-empresa UX + roles por membresía | ✅ Cerrado jul 2026 |
| #0b–#0d | SII/VeriFactu/FACe/SEPA producción | Homologación AEAT/banco |

**Nota:** #28 (PSD2/open banking), #38 (multi-moneda Billing), #39 (portal B2B), #42e (export gestoría ZIP completo) y #42f (servicios recurrentes) ya están ✅ cerrados en código — versiones anteriores de esta tabla mezclaban números de ítem incorrectos.

**Qué queda para 100% nominal (no accionable sin negocio/externo):**

1. **Homologación AEAT/banco** (#0b–#0d).
2. **Producto** (#38–#42f): tabla «Techo alcanzado» arriba.
3. **RLS ampliado** (#34): más tablas además de `Companies` (opcional).
4. **Frontend residual** (#50, lint): mejoras cosméticas no bloqueantes.

## Contra-auditoría jul 2026 — verificación externa e independiente

Diez agentes de auditoría en paralelo (no reutilizaron entre sí resultados,
cada uno verificó desde cero leyendo el código) re-comprobaron con
escepticismo las afirmaciones de cierre de este mismo ADR y trazaron los
flujos de negocio principales de los 9 módulos de punta a punta
(frontend → endpoint → handler → base de datos). Los ajustes de wording y
las reaperturas puntuales ya se aplicaron arriba, en el punto exacto del
hallazgo original (items 3c, 7/CQRS, 13, 32, 42c, 42e). Esta sección añade
los hallazgos que no correspondían a ningún ítem previo — bugs y huecos
reales no vistos por ninguna auditoría anterior de este ADR.

| # | Hallazgo | Módulo | Prioridad |
|---|---|---|---|
| 66 | ~~**Doble descuento de stock en el ciclo de venta completo**~~ **✅ Corregido (jul 2026)**: `InvoiceApprovedEvent` incluye `SalesOrderId` resuelto vía `IBillingInvoiceSalesLinkQuery` (`BillingInvoiceSalesLinkQuery` en Sales.Infrastructure) al bloquear la factura fiscal; `InvoiceApprovedInventoryHandler` omite el decremento cuando `SalesOrderId` está presente (el stock ya se descontó en `DeliveryNoteInventoryHandler`). Facturas directas de Billing sin pedido de venta siguen decrementando en el lock. Tests: `InvoiceApprovedInventoryHandlerTests` | Sales ↔ Inventory ↔ Billing | ✅ Corregido |
| 67 | ~~**Purchasing: los formularios de recepción y factura de proveedor no pueden completarse desde la UI real**~~ **✅ Corregido (jul 2026)**: `purchasing/receipts/new` e `invoices/new` cargan líneas del pedido aprobado (`GET /api/v1/purchasing/orders/{id}`) con `purchaseOrderLineId`/`productId`/`unitPrice`; envían `purchaseOrderId` y campos obligatorios del comando real. `PurchaseOrderLineDto` expone `Id` de línea. Schemas Zod actualizados | Purchasing (frontend) | ✅ Corregido |
| 68 | ~~**Purchasing: los listados de recepciones y facturas de proveedor siempre están vacíos**~~ **✅ Corregido (jul 2026)**: `GetAllGoodsReceiptsQuery`/`GetAllSupplierInvoicesQuery` + `GET` list en `ReceiptsController`/`InvoicesController`; `GetGoodsReceiptQuery`/`GetSupplierInvoiceQuery` sustituyen stubs `GET {id}`. Tests: `GetGoodsReceiptsQueryHandlerTests` | Purchasing | ✅ Corregido |
| 69 | ~~**Purchasing→Accounting: el three-way match nunca genera asiento contable**~~ **✅ Corregido (jul 2026)**: `CreateSupplierInvoiceHandler` publica `SupplierInvoiceCreatedEvent` tras el three-way match; `SupplierInvoiceCreatedEventHandler` (Accounting) crea asiento 600/472/410 con `SourceType=SupplierInvoice` (mismo patrón que `ExpenseApprovedEventHandler`, sin duplicar lógica inline). Tests: `SupplierInvoiceCreated_CreatesBalancedJournalEntry_600_410`, `CreateSupplierInvoiceHandlerTests` verifica publicación del evento | Purchasing ↔ Accounting | ✅ Corregido |
| 70 | ~~**RLS (Row-Level Security) está apagado por defecto fuera de Docker Compose**~~ **✅ Corregido (jul 2026)**: `Postgres:RlsEnabled=true` en `appsettings.json`, `appsettings.Development.json` y `k8s/deployment.yaml`; `PostgresRlsBootstrap` emite `LogWarning` explícito si RLS está apagado; `PostgresRlsBootstrapTests` ya no hace `Assert.True(true)` — en CI falla si Docker no está disponible, en local sale sin verificar (no falsa aserción). Helper `IntegrationTestDocker` | Core (multi-tenant) | ✅ Corregido |
| 71 | ~~**`CrmDbContext` es el único DbContext de los 9 módulos sin `AuditSaveChangesInterceptor` registrado**~~ **✅ Corregido (jul 2026)**: `Modules/Crm/Infrastructure/DependencyInjection.cs` registra `AuditSaveChangesInterceptor` igual que el resto de módulos | Crm | ✅ Corregido |
| 72 | ~~**`PosTerminalsController` (TPV físico) tiene backend completo y cero frontend**~~ **✅ Corregido (jul 2026)**: pestaña "TPV" en `TreasuryClient.tsx` conectada a `GET/POST /api/proxy/treasury/pos-terminals` y `POST .../{id}/payments`; listado de terminales, alta y registro de cobro con tarjeta | Treasury (frontend) | ✅ Corregido |
| 73 | ~~**Test que no verifica comportamiento real**: `Billing/MultiCurrencyAndPosTests.cs` → `CreateInvoiceMultiCurrencyTests.ExchangeRateLookup_ConvertsUsdToEur` solo instanciaba el propio `FakeExchangeRateLookup`~~ **✅ Corregido (jul 2026)**: test trivial eliminado; `CreateInvoicePostgresTests.PostInvoice_WithUsdCurrency_PopulatesMultiCurrencyFields` ejercita `CreateInvoiceHandler` real vía POST `/api/invoices` con divisas sembradas en Treasury (`ExchangeRateLookupAdapter` → `ExchangeRateService`) | Billing (tests) | ✅ Corregido |

**Nota de proceso**: los ítems 66–68, 69, 70, 71, 72 y 73 quedaron cerrados en jul 2026. Siguen abiertos solo ítems bloqueados por externo (#0b–#0d homologación AEAT/banco) y opcionales de producto (LLM #40, push móvil #42, #42d S3).


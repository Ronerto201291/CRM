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
**5 de 43 controllers en todo el backend no inyectan `IMediator`/`ISender`**
(eran 26; Treasury #9, AccountingExport #4, Inventory completo, Billing
`FacturaEController`/`PublicInvoicesController`, Payroll #10, etc.) y
en su lugar inyectan el DbContext del módulo directamente, con lógica de
negocio en el método del controller:
- Accounting: 5 de 16 controllers (`AeatModelsController`,
  `AgingController`, `FinancialStatementsController`,
  `InversionSujetoActivoController`, `IvaManagementController`) — stubs mock.
  `AccountingExportController` ✅ (#4); `RecargoController` en #3b; `ViesController` en #5.
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
- El hallazgo de mayor volumen histórico fue el bypass de CQRS en controllers
  (quedan 5/43 sin `IMediator`, todos stubs mock de Accounting). Cualquier
  trabajo futuro en Accounting debería migrar el controller tocado a MediatR
  en vez de añadir más lógica al patrón existente.
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
| 0a | **VeriFactu — la huella (hash) no coincidiría con la que recalcula la AEAT**: hashea 11 campos en vez de los 8 del Anexo II del RD 1007/2023 (de más: NIF software, IdSistema, número de registro), y los concatena sin claves (`valor&valor` en vez de `clave=valor&clave=valor`). Además: factura simplificada hasheada como F2 pero declarada F1 en el XML; XML con forma de SII en vez de VeriFactu (falta `TipoHuella`/`IDVersion`, encadenamiento nunca declara `PrimerRegistro`); `AceptadoConErrores` tratado como fallo total; reenvíos duplicados del mes completo; sin registro de anulación; sin tabla de auditoría de envíos; falta la leyenda legal obligatoria en el PDF | Billing/Core (VerifactuService, VerifactuXmlGenerator, LockInvoiceHandler) | **Crítica** |
| 0b | **SII — tres bugs independientes garantizan rechazo**: namespace único mal aplicado en `Cabecera`/`Titular`; el sobre SOAP anida el documento dos veces (`signedXml` ya es un `<SuministroLRFacturasEmitidas>` completo, se vuelve a envolver en otro) más la declaración `<?xml?>` incrustada a mitad del sobre; falta `Contraparte` en facturas emitidas y en recibidas se usa `CuotaRepercutida` en vez de `CuotaSoportada` (el campo ni existe en el modelo). La firma XAdES-BES tiene además un problema de orden de operaciones que probablemente la invalida | Core (SiiXmlGenerator, SiiSigningService, SiiSubmissionService) | **Crítica** |
| 0c | **FacturaE — se presenta como firmado sin estarlo**: el fichero se nombra `.xsig` (implica firmado) pero nunca se genera ningún `ds:Signature`; el comentario del controller afirma "cumple el esquema oficial" sin ser cierto. Namespace probablemente incorrecto, bloque `Extensions` mal formado, dirección con placeholders hardcodeados (`Town="N/D"`), sin ningún camino de envío a FACe | Billing (FacturaEService, FacturaEController) | **Crítica** — es el único de los tres que además se anuncia como "cumple el esquema" sin serlo |
| 0d | **SEPA — código muerto e inválido si se conectara**: `GenerateCreditTransferXml` no lo invoca nada del backend (confirmado, cero llamadas). Si se conectara, tampoco sería válido: usa nombres de elemento en inglés (`GroupHeader`) en vez de los códigos ISO 20022 (`GrpHdr`), un paréntesis mal cerrado anida mal la jerarquía, sin validación de IBAN (mod-97), inventa un BIC placeholder inválido cuando falta, y solo genera transferencias (TRF) cuando el caso de uso documentado es cobrar (requeriría SEPA Direct Debit) — de hecho mapea empresa=pagador/cliente=cobrador, el sentido invertido de "cobrar" | Treasury (SepaService) | Alta |
| 0e | **Stripe — sin idempotencia de eventos de webhook**: no hay tabla de eventos procesados ni comprobación de `stripeEvent.Id` — Stripe reentrega eventos por diseño, así que una reentrega de `payment_succeeded` puede volver a extender `ExpirationDate` sin control. Añadir además distinción test/live de la clave API (no existe ninguna) | Core (StripeService) | Alta — la verificación de firma del webhook sí está bien hecha, esto es idempotencia, no autenticación |
| 0f | **Sin validación de NIF/CIF/NIE en ningún sitio del backend** (confirmado por `grep`: cero funciones de validación de dígito de control, solo una heurística de primer carácter en `FacturaEService.IsCompanyNif` que decide persona física/jurídica, no valida nada). Afecta a la vez a CRM, Billing, FacturaE, SII y VeriFactu — un NIF con letra de control equivocada se acepta sin aviso en el alta | CRM/Billing/Core | Alta |

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
| 3c | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `FinancialStatementsController`, `AgingController` sin `IMediator` | Accounting | Deprioritizado — ver nota |
| 4 | `AccountingExportController` (SRP, ~350 líneas tras extracción) — el motivo estructural del acoplamiento cross-módulo está en el ítem 19b | Accounting | ✅ Corregido — 16/16 rutas vía `IMediator`; controller delgado; `modelo347-aeat-txt` en `ExportModelo347AeatTxtQuery` + `IModelo347Exporter.ExportAeatTxtAsync` |
| 5 | `ViesController` (Accounting) sigue duplicando lo que ya resuelve `Erp.Api/TaxController` | Accounting | ✅ Corregido |
| 6 | Validators de FluentValidation nunca registrados por módulo (`AddValidatorsFromAssembly` ausente) | Crm | ✅ Corregido |
| 7 | N+1 en `CreateGoodsReceiptHandler` / `CreateDeliveryNoteHandler` | Purchasing / Sales | ✅ Corregido |
| 8 | Paginación ausente en `Get*Query` (CRM, Accounting, Treasury, Billing, Inventory) | Varios | ✅ Corregido — CRM, Billing, Inventory, Accounting (`GetJournalQuery`) y Treasury (`GetBankMovementsQuery`, `GetCashEffectsQuery`, `GetPaymentOrdersQuery`) paginados con `X-Total-Count` |
| 9 | Los 5 controllers de Treasury sin `IMediator` (incluye parser CSV inline) | Treasury | ✅ Corregido — los 5 controllers del módulo delegan en handlers (`TreasureHandlers`, `GuaranteeHandlers`, `FinancingHandlers`, `ConsolidationHandlers`) |
| 10 | Payroll sin capa CQRS/MediatR | Payroll | ✅ Corregido — `PayrollController` solo `IMediator`; `Features/{Employees,Settlements,Exports}/`; exportes TC1/TC2/RED en `PayrollExportHandlers.cs` |
| 11 | `PurchaseOrdersController` / `SalesOrdersController` sin `IMediator` | Purchasing / Sales | ✅ Corregido — handlers en `Application/Features/Orders/`; controllers delgados; filtro por tenant en queries |
| 12 | Sales y Purchasing: un solo assembly, sin frontera de capas real (confirmado: sin violación activa hoy — es un riesgo latente porque nada impide que el próximo cambio la introduzca, al no haber frontera de compilador dentro del módulo) | Purchasing / Sales | Pendiente |
| 13 | `Erp.Infrastructure` depende de 5 módulos (dirección invertida) — evidencia exacta: `Erp.Infrastructure/Erp.Infrastructure.csproj:5-9` referencia `Modules/{Inventory,Billing,Crm,Accounting,Expenses}/Application` | Core | Pendiente |
| 14 | Entidades núcleo fuera de su módulo: Inventory (`Product`, `Warehouse`, `Stock`, `StockMovement` en `Erp.Domain/Modules/Inventory/Entities/`) **y Accounting, que es más grave de lo documentado — no son 2 entidades sino 6**: `Account`, `JournalEntry`, `JournalEntryLine`, `FiscalPeriod`, `FixedAsset`, `DeferredEntry`, todas en `Erp.Domain/Entities/Accounting/`, mapeadas por `AccountingDbContext`. Nota positiva verificada: pese a esto, cada módulo migra su propio esquema de forma independiente — no hay historial de migraciones compartido ni conflictivo (10 `DbContext`/carpetas `Migrations` separadas, una por módulo + core) | Inventory, Accounting | Pendiente |
| 15 | `ConsolidationController.ConsolidateGroup` responde éxito sin consolidar nada; `ConsolidatedFinancialStatements` nunca se escribe | Treasury | Pendiente |
| 16 | `ExchangeRateRefreshJob` no hace nada nunca: corre fuera de contexto HTTP y `TenantContext.TenantId` siempre es `null` ahí, sin log ni error | Treasury | Pendiente |
| 17 | `frontend/billing/facturae/page.tsx` — página 100% mock, botones sin `onClick`, mientras el backend (`FacturaEController`) sí es real | Billing (frontend) | ✅ Corregido |
| 18 | `frontend/accounting/iva-registers/page.tsx` — página 100% mock con cifras inconsistentes entre cabecera y tabla | Accounting (frontend) | ✅ Corregido — resumen desde `/api/proxy/invoices` y `/api/proxy/expenses/documents`; descarga CSV real vía `libro-iva-emitidas`/`libro-iva-recibidas`; enlace a `/sii` para envío |
| 19 | `OutboxMessageProcessorJob.cs` — implementación completa y correcta del procesador de Outbox, pero huérfana: nunca se registra, existe un duplicado distinto que sí corre (`OutboxProcessorJob.cs`) | Core | Pendiente |
| 19a | **Dependencia circular real entre dos módulos**: `Modules/Crm/Api/Erp.Modules.Crm.Api.csproj:15` referencia `Modules/Expenses/Application`, y a la vez `Modules/Expenses/Api/Erp.Modules.Expenses.Api.csproj:15` referencia `Modules/Crm/Application` — el grafo de ensamblados de Crm y Expenses se referencia mutuamente. Un cambio en la capa Application de cualquiera de los dos puede romper el build del otro sin que sea obvio por qué | Crm ↔ Expenses | Pendiente |
| 19b | `Accounting.Api` referencia por `ProjectReference` directo la Application/Domain de otros 4 módulos (`Erp.Modules.Accounting.Api.csproj:16-20` → Billing, Crm, Expenses y Payroll Application + Payroll Domain), solo para que `AccountingExportController` (ítem 4) pueda inyectar sus 4 `IXxxDbContext` a la vez | Accounting, Billing, Crm, Expenses, Payroll | Pendiente |
| 19c | Acoplamiento directo sin abstracción compartida, en ambas direcciones hacia Accounting: Treasury inyecta `IAccountingDbContext` en `BankReconciliationService.cs:20,26` y `TreasureHandlers.cs:235,240,262` (lee `JournalEntryLines` directamente); Payroll inyecta la clase concreta `AccountingService` (no una interfaz) en `FinalizeSettlementHandler` | Treasury, Payroll, Accounting | Pendiente |
| 19d | `ExpensesController` inyecta `ICrmDbContext` y escribe directamente en `_crmCtx.ActivityLogs` (`Modules/Expenses/Api/Controllers/ExpensesController.cs:3,23,31,72`) — un módulo de negocio escribiendo en la tabla de otro sin pasar por su API/eventos. Contraste positivo ya existente en el propio código: los `EventHandlers` de Inventory y Crm (`ExpenseApprovedInventoryHandler`, `QuoteAcceptedCrmHandler`, etc.) sí reaccionan a eventos de MediatR sin inyectar el DbContext ajeno — ese es el patrón a copiar aquí | Expenses, Crm | Pendiente |
| 19e | No existe ninguna abstracción `IModule`/self-registro: añadir un módulo 10 exige tocar `Program.cs` a mano en 6 sitios distintos (referencias de proyecto, `using`, `AddApplicationPart`, `Add<Módulo>Infrastructure`, bloque `AddMediatR`, `MigrateAsync`) — nada obliga a no olvidar uno (la ausencia de `AddMediatR` en Payroll, ya documentada arriba, es consecuencia directa de esto). Además, `Modules/Accounting/Application/DependencyInjection.cs` define `AddAccountingModule()` (con su propio `AddMediatR` interno) que **nunca se invoca** — es el único módulo con este archivo y quedó como código muerto cuando `Program.cs` empezó a registrar el `AddMediatR` de Accounting directamente | Todos (Core) | Pendiente |
| 19f | Inventory usa `API` en vez de `Api` no solo como nombre de carpeta sino en el namespace real compilado: `namespace Erp.Modules.Inventory.API.Controllers` en sus 5 controllers, referenciado explícitamente en `Program.cs:56` (`typeof(Erp.Modules.Inventory.API.Controllers.ProductsController)`) — inconsistente con los otros 8 módulos (`Erp.Modules.<Nombre>.Api.Controllers`) | Inventory | Baja |
| 19g | Cosmético, agrupado: carpeta de migraciones de Purchasing en `Modules/Purchasing/Migrations/` en vez de `Modules/Purchasing/Infrastructure/Migrations/` (como los otros 8 módulos); `Modules/Sales/Infrastructure/DependencyInjection.cs` usa namespace de bloque (`namespace X { }`) en vez de namespace de archivo (`namespace X;`) como el resto del código | Purchasing, Sales | Baja |

**Verificaciones limpias de esta auditoría de modularidad (sin hallazgos, se deja constancia explícita para no repetir el análisis):** ningún módulo con proyectos separados (los 7 que no son Sales/Purchasing) tiene su Domain o Application referenciando su propia Infrastructure; no se encontró ninguna otra entidad de negocio (Billing, Crm, Expenses, Payroll, Purchasing, Sales, Treasury) mal ubicada en `Erp.Domain` más allá de Inventory y Accounting (ítem 14); el frontend (`frontend/src/app/`) está limpiamente separado por módulo en carpetas, sin ningún import cruzado entre carpetas de módulos distintos.

**Ítems 20+: mejoras funcionales (nivel producto).** A diferencia de 1-19
(deuda de código: duplicación, CQRS, estructura), estos son huecos
funcionales — el ERP hace CRUD correcto por módulo pero no los conecta como
procesos de negocio reales, o tiene funcionalidad fiscal/financiera
simulada en vez de real. Alcance y esfuerzo mayores; no son "arreglar algo
roto" sino "construir la integración/lógica que falta". Priorizados por
relación esfuerzo/impacto (los primeros reutilizan código que ya existe).

| # | Mejora | Módulos | Prioridad |
|---|---|---|---|
| 20 | `GoodsReceipt` (Purchasing) no incrementa stock en Inventory al recibir mercancía — sin esto, "recepción de compra" no tiene efecto real en el almacén | Purchasing → Inventory | Alta |
| 21 | `DeliveryNote` (Sales) no decrementa stock en Inventory al entregar — sin esto, "entrega de pedido" no descuenta existencias | Sales → Inventory | Alta |
| 22 | `Sales.CustomerInvoice` y `Billing.Invoice` son dos sistemas de facturación de cliente paralelos y desconectados; hay que decidir cuál es la fuente de verdad fiscal (Billing tiene hash-chain/cumplimiento antifraude, Sales no) y conectar Sales a Billing en vez de duplicar | Sales ↔ Billing | Alta |
| 23 | `SalesOrder.ClientId`/`ClientName` es texto suelto sin FK real a `Crm.Client` — Sales no está realmente integrado con CRM | Sales ↔ Crm | Media |
| 24 | Implementar Modelo 303 (IVA trimestral) real a partir de `VatTransaction`/`JournalEntry`, sustituyendo el stub de `AeatModelsController` | Accounting | Alta (es la declaración que más pymes presentan) |
| 25 | Implementar Modelo 347 (operaciones anuales >3.005,06€) real, sustituyendo el stub | Accounting | Media |
| 26 | Estados financieros reales (cash-flow, cuenta de resultados) generados desde `JournalEntry`/`JournalEntryLine`, sustituyendo `FinancialStatementsController` | Accounting | Media |
| 27 | Activar el motor de automatización: registrar `RuleEvaluatorJob` en Hangfire, hacer que `CreateRuleCommand` persista, conectar `settings/automation` al backend — la lógica de negocio (facturas vencidas, stock bajo) ya existe, solo falta cablearla | Core/Automatización | Alta (bajo esfuerzo, ya construido) |
| 28 | Sustituir la importación manual de CSV bancario por integración de banca abierta (PSD2) para conciliación en tiempo real | Treasury | Media |
| 29 | Payroll: integración real con Sistema RED/Seguridad Social, más allá de los exports TC1/TC2 "orientativos" actuales | Payroll | Media |
| 30 | Flujos de aprobación (pedidos de compra o gastos por encima de un umbral, antes de confirmar/contabilizar) — no existe ningún mecanismo de aprobación en el código hoy | Purchasing/Expenses | Media |
| 31 | Conectar el interceptor de Audit Log (`AuditInterceptor.cs`, ya documentado como código muerto en el catálogo de mock de arriba) — de cara al usuario el sistema aparenta tener auditoría inmutable y hoy no la tiene | Core | Alta (credibilidad del producto, no solo código) |

**Ítems 32+: mejoras adicionales de plataforma y producto**, identificadas al
cierre de esta auditoría (no se derivan de un hallazgo puntual del código,
sino de comparar el estado actual contra lo que exigiría un ERP de clase
mundial). Mezcla código/plataforma (32-37) y producto (38-42).

| # | Mejora | Módulos | Prioridad |
|---|---|---|---|
| 32 | Cero tests automatizados en todo el repo — priorizar tests de integración sobre los flujos críticos (facturación, asientos automáticos, aislamiento multi-tenant) antes que cobertura exhaustiva | Todos | Alta |
| 33 | No existe middleware global de manejo de excepciones — cualquier excepción no controlada (incluida la `ValidationException` de FluentValidation recién activada en CRM) se filtra como un 500 crudo sin `ProblemDetails` ni contrato de error consistente | Core | Alta |
| 34 | Aislamiento multi-tenant a un solo nivel de defensa (global query filters de EF Core); añadir Row-Level Security de Postgres como segunda barrera — el fallo más grave posible en un SaaS es fuga de datos entre empresas | Core | Alta |
| 35 | Ninguna de las 19 violaciones de arquitectura de este ADR se detecta automáticamente en CI; añadir tests de arquitectura (tipo NetArchTest: "ningún controller referencia DbContext directamente", "Domain no depende de Infrastructure") para que las reglas se apliquen solas en cada PR | Core/CI | Media |
| 36 | Sin observabilidad real: no hay logging estructurado, tracing distribuido ni métricas en ningún módulo — depurar producción (p. ej. por qué se atascó el outbox) hoy depende de logs de consola sueltos | Core | Media |
| 37 | Frontend con muy poca reutilización de componentes — ver auditoría dedicada y desglose en ítems 43-51 | Frontend | Media |
| 38 | Multi-moneda real en Billing (facturar en divisa distinta del euro con conversión automática usando los tipos de cambio de Treasury) — hoy no está claro que Billing soporte esto | Billing ↔ Treasury | Media |
| 39 | Portal de autoservicio para cliente/proveedor (ver y pagar facturas, subir facturas de proveedor) — hoy todo el flujo es interno, sin reenvío manual de PDFs | Billing/Purchasing | Baja |
| 40 | Funciones asistidas por IA sobre los datos ya capturados: detección de anomalías en gastos, previsión de tesorería, categorización automática — extensión natural del OCR real que ya existe en Expenses | Expenses/Treasury | Baja |
| 41 | Asistente de alta/onboarding: plantillas de plan contable por sector, importación desde Excel/otro ERP — hoy el alta de empresa no tiene ninguna ayuda guiada | Core | Baja |
| 42 | Notificaciones proactivas (email/push) de facturas vencidas, stock bajo, aprobaciones pendientes — depende directamente de activar el motor de automatización (ítem 27) | Core/Automatización | Media |
| 42a | **Multi-empresa real (un mismo login con acceso a varias `Company`)**: hoy el modelo es 1 `User` = 1 `Company` de forma rígida (`User.CompanyId` es una FK única, sin tabla `UserCompany`), email único a nivel de toda la plataforma (`RegisterCompanyCommand.cs:54-56`), y `CompanyId` grabado como claim fijo e inmutable en el JWT al hacer login (`JwtProvider.cs:33`) — no hay ningún concepto de "empresa activa" cambiable en sesión. Esto bloquea el caso de uso más relevante para el público pyme/gestoría al que apunta el producto: una gestoría que lleva la contabilidad de varias pymes clientas necesitaría hoy una cuenta (email+contraseña) distinta por cada empresa cliente, sin ninguna vista ni cambio de contexto conjunto. Requiere: tabla `UserCompany` (muchos-a-muchos, con rol por membresía) en sustitución/complemento de `User.CompanyId`; relajar la unicidad de email a `(Email, CompanyId)`; JWT/sesión con lista de empresas accesibles + "empresa activa" cambiable sin re-login (el `TenantContext.tsx` del frontend ya tiene `setTenant()`/`clearTenant()` pero hoy nadie los invoca — sería la pieza a activar); alta self-service de "otra empresa" desde una cuenta ya existente (hoy crear `Company` solo lo hace el admin raíz vía `settings/empresas` o el registro inicial); y decidir el modelo de suscripción (¿un plan que cubra N empresas para gestorías, en vez de 1 suscripción por `Company` como hoy?). **Importante para el orden de implementación**: no construir esto reutilizando las tablas de consolidación de grupo de Treasury (`ConsolidationGroup`/`SubsidiaryCompany`, ítem 15) — están mock y no tienen ninguna relación con `User`/auth; son un problema distinto (consolidación contable de un grupo empresarial) que solo tiene sentido abordar después de que exista multi-empresa real a nivel de acceso | Core (User/Company/Auth) | **Alta** — es el caso de uso que justifica el modelo de distribución vía gestorías en el mercado pyme español |

| 42b | **Conciliación de ingresos por método de pago (TPV, transferencia, Bizum, caja)** — hoy `PaymentMethod` es un string libre sin restricción (`InvoiceStatusCommands.cs:8`, default `"bank"`), y solo hay una distinción binaria efectivo/banco en el asiento contable (`PaymentReceivedEventHandler.cs:51`: `notification.PaymentMethod == "cash" ? "570" : "572"`) — ni Bizum ni TPV tienen cuenta/tratamiento propio, y no existe ninguna entidad de sesión/arqueo de caja (`grep` de `CashSession`/`CashRegister`/`ArqueoCaja` en todo el backend: 0 resultados). Para comercio/hostelería español, donde la mayoría del ingreso diario entra por TPV/Bizum/efectivo y no por transferencia, esto es una carencia real: el TPV liquida neto de comisión días después y hay que casarlo con las ventas, Bizum liquida en el banco pero sin vincularse al pedido de origen, y no hay forma de abrir/cerrar caja con esperado vs. contado. Reutilizar `BankReconciliationService` (Treasury) para la conciliación de TPV/Bizum en vez de duplicar lógica de conciliación nueva | Billing ↔ Treasury | **Alta** |
| 42c | Cruce entre módulo contratado y permiso de usuario (nota: este ítem es sobre módulos de la plataforma SaaS por empresa/usuario — no confundir con el ítem 42f, que es sobre servicios que la pyme le vende a **sus** clientes) — `TenantModule` (`CompanyId`, `ModuleName`, `IsEnabled`) y el toggle de `settings/subscription` ya son reales y funcionan (no mock, verificado: `PUT /api/proxy/tenant/modules/{id}` real), pero la granularidad es solo a nivel empresa. Falta cruzar esto con roles/permisos por usuario (ADR-0002) para poder decir "el módulo está contratado por la empresa, pero este usuario no tiene acceso" — hoy si el módulo está activado, cualquier usuario de esa empresa con el permiso genérico lo ve | Core (Licensing + Auth) | Media |
| 42d | Biblioteca interna de documentos — no existe ninguna entidad de documento genérica hoy (0 resultados de `DocumentLibrary`/`DocumentRepository` en el backend); cada módulo guarda sus propios archivos de forma aislada (OCR de gastos, PDFs de factura). Añadir una entidad `Document` ligada a `CompanyId` + enlace opcional a Cliente/Proveedor/Factura/Gasto, reutilizando el volumen `uploads` que ya existe en Docker Compose — empezar acotado (documentos ligados a una entidad de negocio), no un gestor documental genérico de carpetas | Core | Media |
| 42e | Envío periódico de documentos a gestorías externas — no existe ningún mecanismo de exportación/envío por email hoy (`grep` de "gestoria"/"ExportToAccountant" en el backend: 0 resultados). Útil de forma independiente al ítem 42a (multi-empresa): no toda pyme tendrá una gestoría usando este mismo ERP, así que un export periódico (ZIP/PDF con libro de IVA + facturas + gastos del periodo) con envío automático configurable sigue aportando valor aunque se construya 42a | Billing/Accounting | Baja — depende de decidir primero el alcance de 42a |
| 42f | **Servicios contratados por cliente (catálogo de servicios propio de cada empresa + contrato recurrente por cliente)**: no existe ningún concepto de esto hoy — confirmado por `grep`, 0 resultados de `RecurringInvoice`/`ContractedService`/`ServiceContract` en todo el backend. Caso de uso real: un taller que vende a un cliente concreto un servicio de mantenimiento anual, y ese contrato tiene que quedar enlazado con el cobro (Treasury/Billing) y con el asiento contable correspondiente (Accounting) cada vez que se genera/cobra. El catálogo de servicios ofrecidos **varía por empresa** (un taller no vende los mismos servicios que una gestoría o una clínica), así que no puede ser un catálogo fijo global — necesita: (1) un catálogo de servicios propio por `CompanyId` (análogo a `Product` de Inventory pero para servicios, no bienes físicos con stock), (2) una entidad `ClientContractedService` que enlace `Crm.Client` + servicio del catálogo + periodicidad/importe/fecha de alta-renovación, (3) generación automática de factura recurrente en Billing al vencer cada periodo (hoy Billing no tiene ningún job de facturación recurrente, solo `ExpireQuotesJob` para presupuestos caducados), y (4) el asiento contable automático correspondiente al cobrarse, igual que ya hace `PaymentReceivedEventHandler` para facturas puntuales. Depende de decidir primero si esto es parte de Billing, de CRM o un módulo propio — tiene relación directa con ambos | Billing ↔ Crm | Alta — es un caso de uso de negocio real y concreto para el público objetivo (taller, gestoría, clínica con contratos de mantenimiento/servicio recurrente) |
TypeScript + Tailwind), mismo nivel de detalle que la auditoría de backend,
con cita de archivo para cada hallazgo — amplía y sustituye al ítem 37.

| # | Mejora | Evidencia | Prioridad |
|---|---|---|---|
| 43 | 71 de 72 `page.tsx` son Client Components (`"use client"` + `useEffect`+`fetch`) — no se aprovecha ninguna ventaja de Server Components/Server Actions del App Router (fetch en servidor, menos JS al cliente, streaming) | Patrón idéntico en los 9 módulos revisados, p. ej. `sales/orders/page.tsx:24-40`, `treasury/currencies/page.tsx:16-36` | Media (es un cambio de patrón transversal, no un bug puntual) |
| 44 | Fugas de `any` pese a `strict: true` en `tsconfig.json`: 24 `: any`, 8 `as any`, 11 `any[]` en 19 archivos — incluye un escape-hatch repetido `(lines[i] as any)[key] = val` en 8 formularios de líneas de pedido/factura | `dashboard/page.tsx:12-13`, `expenses/[id]/page.tsx:38`, `billing/quotes/page.tsx:87`, y 7 archivos más con el mismo patrón | Media |
| 45 | Sin `error.tsx`/`loading.tsx`/`not-found.tsx` en todo `src/app/` (cero archivos) — cada página gestiona error/carga a mano y de forma inconsistente; algunos fetches fallidos fallan en silencio (solo `console.error`), 91 usos de `alert()` nativo para feedback de error/validación | `crm/clients/[id]/page.tsx:38-51` (falla en silencio) vs. `:56-85` (sí muestra error) en la misma página; `accounting/cash-flow/page.tsx:6-14` sin try/catch ni `res.ok` | Media |
| 46 | **Proxy abierto de facto**: `api/proxy/[...path]/route.ts` define `PROXY_PATHS` como aparente whitelist pero nunca se usa — cualquier request autenticada se reenvía a `${backendUrl}/api/${path}` para cualquier ruta del backend, sin restricción real | `src/app/api/proxy/[...path]/route.ts` — ahora `ALLOWED_PATH_PREFIXES` se valida con `isPathAllowed()` antes de reenviar; rutas no listadas devuelven 403 | ✅ Corregido |
| 47 | Bug real en el proxy: en `proxyFetch`, `response` se declara dentro del `try` pero el `catch` la referencia (`response.headers.get(...)`) — si el `fetch` falla (backend caído, DNS), el catch lanza `ReferenceError` en vez de devolver el JSON de error esperado | `src/app/api/proxy/[...path]/route.ts` — el `catch` ya no referencia `response`; devuelve 502 con mensaje de conexión | ✅ Corregido |
| 48 | Accesibilidad mínima: 0 atributos `aria-*` en todo `src/`, 0 `role="dialog"` en los 8+ modales existentes (sin focus trap ni cierre con Escape), 0 `htmlFor` en 51 archivos que usan `<label>` (sin asociación programática label↔input) | `crm/prospects/page.tsx:272`, `crm/leads/page.tsx:134`, `crm/clients/[id]/page.tsx:125-139` | Media |
| 49 | Sin caché ni estado compartido: existen dos abstracciones de fetch ya construidas (`hooks/useApi.ts`, `lib/api.ts`) con 0 usos — cada página hace su propio `fetch` inline; 6 páginas distintas piden `/api/proxy/clients` completo de forma independiente en cada navegación | `hooks/useApi.ts`, `lib/api.ts` (código muerto); `crm/page.tsx`, `crm/alerts`, `billing/quotes`, etc. | Media |
| 50 | 0 usos de `useMemo`/`React.memo` en todo `app/` — p. ej. `crm/page.tsx` (316 líneas) refiltra 3 listas completas en cada pulsación de tecla del buscador, sin memoización | `crm/page.tsx:77-79,91` | Baja |
| 51 | Sin librería de formularios/validación (0 uso de react-hook-form/Zod/Formik) — "validación" es solo comprobar campos no vacíos vía `if`+`alert()`, sin reflejar las reglas reales del backend (FluentValidation); mismo patrón superficial repetido en 18+ formularios | `crm/clients/[id]/page.tsx:57-60`, `treasury/currencies/page.tsx:39`, y 16 archivos más | Media |

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
| 52 | Desajuste de puerto 5000 vs. 8080 real — **no era solo un problema del script de servidor**: `.env.example` traía `ASPNETCORE_URLS=http://+:5000`, que también rompía el arranque local (el contenedor expone/healthchequea 8080). ✅ Corregido: `.env.example` ahora usa `8080` y las URLs de frontend apuntan a `8081`/`8080` según corresponda (ver ítem 65) | `deploy/deploy.sh:28-29`, `backend/Dockerfile:39-43`, `.env.example` | ✅ Corregido (parte local); resto del script de servidor sigue Aparcado |
| 53 | TLS desactivado en nginx de producción con HSTS activo (footgun) — solo aplica cuando haya un dominio/servidor real sirviendo HTTPS | `deploy/nginx/erp.conf:5,8-10,16` | Aparcado — sin servidor |
| 54 | Nginx de host y de contenedor compitiendo por 80/443 — parte de `setup-vps.sh`, no del flujo local | `deploy/setup-vps.sh:20,49-50` + `docker-compose.yml:88-89` | Aparcado — sin servidor |
| 55 | Postgres publicado a `0.0.0.0:5432` sin bloqueo de firewall — en local (sin IP pública) no es una exposición real; revisar de nuevo al desplegar en servidor | `docker-compose.yml:16-17` | Aparcado — sin servidor |
| 56 | Backups solo en disco local sin copia offsite — no aplica sin un servidor que respaldar; en local el propio equipo del usuario es el "backup" | `deploy/backup.sh:32-40` | Aparcado — sin servidor |
| 57 | El pipeline de CI/CD nunca hace escaneo de vulnerabilidades (dependencias ni imagen de contenedor) — sigue teniendo sentido aunque no haya despliegue activo, es higiene de repositorio | `.github/workflows/ci-cd.yml` (ambas versiones) | Media |
| 58 | Deploy sin estrategia zero-downtime (`docker compose down` completo) — solo relevante para el script de servidor | `deploy/deploy.sh:16-18` | Aparcado — sin servidor |
| 59 | Dos workflows de CI/CD divergentes; imágenes que CI construye nunca llegan a desplegarse — vale la pena simplificar a un único workflow aunque hoy no despliegue a ningún sitio, para no arrastrar confusión cuando se retome servidor | `.github/workflows/ci-cd.yml` (ambos), `docker-compose.yml` | Media |
| 60 | Sin monitorización externa (logs/alertas/uptime) — solo tiene sentido con algo desplegado que monitorizar | — | Aparcado — sin servidor |
| 61 | Renovación de certificados Let's Encrypt no automatizada — no aplica sin dominio/servidor real | `deploy/setup-vps.sh:50` | Aparcado — sin servidor |
| 62 | El manifiesto de k8s está obsoleto y nunca se ha usado — al no haber servidor tampoco hay presión por mantenerlo; valorar si retirarlo del repo o dejarlo como referencia futura | `k8s/deployment.yaml` completo | Baja |
| 63 | Sin infraestructura como código; `setup-vps.sh` no es re-ejecutable de forma segura — irrelevante mientras no haya VPS que aprovisionar | `deploy/setup-vps.sh` completo | Aparcado — sin servidor |
| 64 | Todo pensado para un único VPS sin redundancia — la pregunta de redundancia/HA no aplica a un entorno Docker local de desarrollo | `docker-compose.yml` completo | Aparcado — sin servidor |
| 65 | Verificar que `docker compose -f docker-compose.yml -f docker-compose.local.yml up` levanta el stack completo en local. **Verificado por partes (ver detalle debajo); build de backend y de frontend confirmados, `docker compose config` confirma el merge correcto, pero el `up` completo con los 5 contenedores corriendo a la vez no se ha podido ejecutar en el sandbox de CI/agente porque bloquea la CDN de Docker Hub — pendiente de confirmación final en una máquina con Docker Hub accesible** | `docker-compose.yml` + `docker-compose.local.yml` + `backend/Dockerfile` + `frontend/next.config.ts` | 🟡 Verificado parcialmente — falta `up` real |

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

**Limitación honesta:** en el sandbox donde se hizo esta verificación, las
imágenes `postgres:16-alpine`, `redis:7-alpine`, `node:20-alpine` y
`nginx:alpine` no se pudieron descargar (la CDN de Docker Hub está bloqueada
por política del proxy de salida del entorno) — por tanto **no se ha podido
ejecutar el `docker compose up` completo con los 5 servicios corriendo a la
vez**. Lo verificado de forma independiente es: (a) el build del backend
completa con éxito, (b) el build/`next build` del frontend completa con
éxito y genera el standalone que el Dockerfile necesita, (c) el merge de
`docker-compose.yml` + `docker-compose.local.yml` es correcto vía
`docker compose config`. Falta la confirmación final de un `up` real, que
debe hacerse en una máquina con acceso normal a Docker Hub.

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
| Accounting | `ViesController` valida NIF-IVA contra el registro VIES de la UE | ✅ Corregido: despacha `ValidateViesCommand` vía `IMediator`, que invoca el mismo `IViesService` SOAP que `TaxController`; el texto `Advice` se centraliza en `ViesResponseMapper` | ADR-0006/0013, backlog #5 |
| Accounting (frontend) | `iva-registers/page.tsx` muestra libros de IVA reales exportables a SII | ✅ Corregido: carga resumen del ejercicio desde facturas/gastos reales; botones descargan CSV vía `/api/proxy/accounting/export/libro-iva-{emitidas,recibidas}`; enlace a `/sii` | backlog #18 |
| Billing (frontend) | `billing/facturae/page.tsx` gestiona documentos FacturaE reales (firmar, enviar a VERI\*FACTU) | ✅ Corregido: lista facturas bloqueadas vía `/api/proxy/invoices`, descarga XML (`/api/proxy/v1/billing/facturae/{id}`) y PDF reales; enlace a `/verifactu` para envío por período | backlog #17 |
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

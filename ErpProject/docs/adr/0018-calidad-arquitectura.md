# ADR-0018: Calidad arquitectÃ³nica transversal (SOLID, Clean Architecture, CQRS, duplicaciÃ³n, escalabilidad)

## Estado
Aceptado â€” auditorÃ­a del cÃ³digo real en `main` a fecha de este ADR. No es un
plan a futuro: cada hallazgo estÃ¡ verificado leyendo el archivo citado.

## Contexto
Los ADR 0001-0017 documentan la estructura de cada mÃ³dulo, pero no evalÃºan
sistemÃ¡ticamente su calidad arquitectÃ³nica. Este ADR cierra ese hueco con una
auditorÃ­a explÃ­cita en siete dimensiones â€” SOLID, Clean Architecture (capas y
direcciÃ³n de dependencias), estructura de carpetas, cÃ³digo duplicado, cÃ³digo
limpio (en particular: ausencia de lÃ³gica de negocio en los controllers),
escalabilidad, y cumplimiento real de CQRS â€” y sirve de referencia Ãºnica para
no repetir este anÃ¡lisis en cada ADR de mÃ³dulo. A partir de este ADR, el
checklist que resume queda incorporado de forma permanente en
`0000-template.md`, para que cualquier ADR nuevo (mÃ³dulo o revisiÃ³n) se
evalÃºe siempre contra los mismos siete puntos.

AdemÃ¡s de la auditorÃ­a arquitectÃ³nica, este ADR mantiene dos catÃ¡logos
transversales que se actualizan a medida que se encuentran o corrigen
hallazgos: un **catÃ¡logo de datos y lÃ³gica simulada (mock)** â€” quÃ© partes
del sistema aparentan funcionar pero no hacen lo que dicen â€” y una lista
separada de **credenciales/secretos hardcodeados**, deliberadamente fuera
del backlog de remediaciÃ³n por ser deuda de seguridad/configuraciÃ³n, no de
arquitectura.

## DecisiÃ³n

### 1. SOLID

**SRP (Single Responsibility) â€” violado en puntos concretos:**
- `backend/Modules/Accounting/Api/Controllers/AccountingExportController.cs`
  (1362 lÃ­neas, 19 endpoints `[HttpGet]`) inyecta seis dependencias de
  contexto repartidas en cinco mÃ³dulos distintos (`IAccountingDbContext`,
  `IExpensesDbContext`, `IApplicationDbContext`, `IBillingDbContext`,
  `ICrmDbContext`, `IPayrollDbContext`) y construye inline la exportaciÃ³n
  CSV/XML de ocho modelos fiscales AEAT (303, 111, 130, 190, 200, 202, 347,
  349, 390). Un Ãºnico archivo mezcla routing, acceso a datos cross-mÃ³dulo y
  serializaciÃ³n de formatos oficiales.
- `backend/Modules/Accounting/Application/Handlers/CloseFiscalYearHandler.cs`
  (487 lÃ­neas): un solo `Handle()` hace cierre de ejercicio fiscal, cÃ¡lculo
  de P&L, generaciÃ³n de asientos de cierre y creaciÃ³n de saldos de apertura.
- Menos grave (organizaciÃ³n de archivo, no de clase): `QuoteHandlers.cs`
  (Billing, 1011 lÃ­neas) y `TreasureHandlers.cs` (Treasury, 706 lÃ­neas)
  concatenan 11-14 handlers bien acotados en un Ãºnico archivo â€” cada clase
  cumple SRP, el archivo no.

**OCP (Open/Closed):**
- `backend/Modules/Accounting/Application/Features/Vat/CalculateVatCommand.cs`
  resuelve tasas de IVA/recargo de equivalencia con un `Dictionary`/`switch`
  sobre literales de texto (`"Standard"`, `"Reduced"`, `"SuperReduced"`) â€”
  aÃ±adir una categorÃ­a fiscal nueva exige editar cÃ³digo, no datos.
- Contraejemplo positivo: `backend/Erp.Infrastructure/Security/ModuleAuthorizationHandler.cs`
  y `PermissionService.cs` resuelven gating de mÃ³dulos y permisos 100%
  contra datos (`Plans`, `PlanModules`, `TenantModules`, `RolePermissions`) â€”
  ningÃºn `switch` por nombre de mÃ³dulo o permiso. Es el patrÃ³n a imitar.

**LSP (Liskov):**
- `backend/Modules/Accounting/Infrastructure/Data/AccountingDbContext.cs`
  sobrescribe `SaveChangesAsync` y aÃ±ade precondiciones nuevas no anunciadas
  por el contrato base `ModuleDbContextBase` (lanza si el perÃ­odo fiscal estÃ¡
  cerrado, o si Debeâ‰ Haber). Es una decisiÃ³n de negocio razonable (RD
  1619/2012), pero un consumidor que solo conozca `IAccountingDbContext` no
  tiene forma de saber que `SaveChangesAsync` puede lanzar por estas razones.
  DesviaciÃ³n pragmÃ¡tica, no un bug.

**ISP (Interface Segregation) â€” violado:**
- `IAccountingDbContext` expone **29 DbSets**; `ITreasuryDbContext`, 20;
  `IApplicationDbContext`, 17. Ejemplo concreto:
  `backend/Modules/Accounting/Application/Queries/GetFiscalPeriodsQuery.cs`
  inyecta el interface completo de 29 DbSets para usar Ãºnicamente
  `FiscalPeriods`. Cualquier cambio en la interfaz obliga a recompilar todos
  sus consumidores, incluidos los de otros mÃ³dulos (Treasury).

**DIP (Dependency Inversion) â€” en general limpio:**
No se encontrÃ³ `new HttpClient(...)`, uso de `DbContext` concreto, ni
service-locator dentro de ninguna capa Application (`Modules/*/Application`,
`Erp.Application`). La Ãºnica grieta relevante es de direcciÃ³n de mÃ³dulos, no
de inversiÃ³n de dependencias en sÃ­ (ver punto 2).

### 2. Clean Architecture y direcciÃ³n de dependencias

**ViolaciÃ³n grave: el core depende de los mÃ³dulos, no al revÃ©s.**
`backend/Erp.Infrastructure/Erp.Infrastructure.csproj` referencia las capas
Application de cinco mÃ³dulos (Inventory, Billing, Crm, Accounting, Expenses).
SÃ­ntoma concreto: `backend/Erp.Infrastructure/Seeding/PgcSeeder.cs` (cÃ³digo
"core") inyecta `IAccountingDbContext` (interfaz de mÃ³dulo). La direcciÃ³n
prevista en ADR-0001 es la contraria: los mÃ³dulos dependen del core, el core
no conoce a los mÃ³dulos.

**Acoplamiento cruzado sin abstracciÃ³n compartida:**
Treasury â†’ Accounting (`TreasureHandlers.cs`, `BankReconciliationService.cs`
inyectan `IAccountingDbContext` directamente), Payroll â†’ Accounting
(`Payroll/Api` referencia `Accounting/Application`), Crm â†” Expenses
(referencias cruzadas de `Application`). Ninguno pasa por un evento/outbox o
una interfaz comÃºn â€” son `ProjectReference` directos entre mÃ³dulos que en
teorÃ­a deberÃ­an ser independientes.

**Dominio limpio (positivo):** cero dependencias de EF Core, Data Annotations
o ASP.NET encontradas en `Erp.Domain/` ni en ningÃºn `Modules/*/Domain/`
(excluyendo `Migrations/`) â€” verificado en toda la base de cÃ³digo.

**Hallazgo nuevo â€” Inventory (âœ… corregido ADR-0018 #14):** `Product`, `Warehouse`, `Stock` y
`StockMovement` viven en `Modules/Inventory/Domain/Entities/` (antes fÃ­sicamente en
`Erp.Domain/Modules/Inventory/` con el namespace correcto pero fuera del mÃ³dulo).

### 3. Estructura de carpetas
7 de 9 mÃ³dulos siguen el patrÃ³n `Api/Application/Domain/Infrastructure` con
cuatro `.csproj` separados (frontera real de compilador entre capas).
Desviaciones:
- **Inventory**: la carpeta es `Modules/Inventory/API` (mayÃºscula) en vez de
  `Api`; namespace `Erp.Modules.Inventory.API.Controllers`. CosmÃ©tico, no
  rompe la capa, pero es una inconsistencia real de nombres.
- **Sales y Purchasing**: un Ãºnico `.csproj` (`Erp.Modules.Sales.Infrastructure.csproj`,
  `Erp.Modules.Purchasing.Infrastructure.csproj`) compila las cuatro carpetas
  lÃ³gicas como un solo assembly. Las carpetas `Api/Application/Domain/Infrastructure`
  existen pero son solo organizativas â€” nada impide en el compilador que
  `Domain` referencie `Infrastructure` dentro de esos dos mÃ³dulos, a
  diferencia de los otros 7. Purchasing ademÃ¡s tiene `Migrations/` en la
  raÃ­z del mÃ³dulo en vez de bajo `Infrastructure/Migrations/`.

### 4. CÃ³digo duplicado
- **Corregido â€” el mismo patrÃ³n corregido en VIES (ver ADR-0006) se repetÃ­a
  en VAT**: `backend/Modules/Accounting/Api/Controllers/VatController.cs`
  tenÃ­a su propio diccionario de tasas de IVA y cÃ¡lculo inline, totalmente
  independiente de `CalculateVatCommand.cs` (que tenÃ­a otro diccionario con
  las mismas tasas mÃ¡s lÃ³gica real de ISP/intracomunitario/recargo y
  persistencia en `VatTransactions`). Ahora `VatController.CalculateVat`
  despacha `CalculateVatCommand` vÃ­a `IMediator`, y ambos (controller y
  handler) leen las tasas de una Ãºnica fuente,
  `Application/Features/Vat/VatRates.cs` (`SpanishVatRates`). De paso se
  corrigiÃ³ que `CalculateVatCommand.CompanyId` viniera del body (el cliente
  podÃ­a enviar cualquier tenant) â€” ahora se resuelve del `ITenantContext`
  del handler, igual que el resto de comandos de Accounting.
- **Corregido â€” mismo patrÃ³n, con un bug real de contrato aÃ±adido**:
  `ProrrataController.CalculateProrrata` calculaba inline con un
  `CalculateProrrataRequest` propio (`DeductibleOperations`/`NonDeductibleOperations`/`TotalVatSupported`),
  totalmente distinto del `CalculateProrrataCommand` huÃ©rfano. Al revisar el
  frontend real (`frontend/src/app/accounting/prorrata/page.tsx`) se
  encontrÃ³ que este envÃ­a/lee `{fiscalYear, inlandRevenue, exemptRevenue,
  type}` / `{inlandRevenue, exemptRevenue, prorrataPercentage}` â€” nombres que
  no coinciden con ninguno de los dos, asÃ­ que la pÃ¡gina producÃ­a
  `TypeError: Cannot read properties of undefined (reading 'toFixed')` al
  pulsar "Calculate Prorrata". Se corrigiÃ³ `CalculateProrrataCommand` para
  usar exactamente el contrato del frontend (calculadora manual: el usuario
  introduce los ingresos, se persiste un `ProrrataCalculation` real) y
  `ProrrataController` ahora despacha ese comando vÃ­a `IMediator`.
- **Corregido** â€” `backend/Modules/Crm/Application/Handlers/ClientHandlers.cs`
  era un set completo de queries/commands (`GetClientsModuleQuery`,
  `CreateClientModuleCommand`, etc.) que ningÃºn controller referenciaba â€” el
  path real es `Application/Features/Crm/*`. La particularidad era que
  `Program.cs` usaba un tipo de ese archivo como "ancla" de assembly para
  registrar MediatR de todo el mÃ³dulo CRM, asÃ­ que el archivo era cÃ³digo
  muerto pero estructuralmente necesario tal cual estaba montado. Se eliminÃ³
  el archivo y el ancla pasÃ³ a `Features/Crm/Handlers/GetClientsHandler`
  (handler real, usado por `ClientsController`).
- Guard clauses de `tenantId` (`_tenant.TenantId ?? throw new InvalidOperationException(...)`)
  copiadas decenas de veces por controller en vez de centralizarse (ejemplo:
  seis copias solo en `TreasuryController.cs`).

### 5. CÃ³digo limpio â€” "nada de lÃ³gica en los controllers"
**0 de 43 controllers en todo el backend quedan sin `IMediator`/`ISender`**
(eran 26; Treasury #9, AccountingExport #4, Inventory completo, Billing
`FacturaEController`/`PublicInvoicesController`, Payroll #10, core legacy
`TaxController`/`SiiController`/`SubscriptionController`/`FiscalCalendarController`, etc.) y
en su lugar inyectaban el DbContext del mÃ³dulo directamente, con lÃ³gica de
negocio en el mÃ©todo del controller:
- Accounting: los 5 stubs mock (`AeatModelsController`, `AgingController`,
  `InversionSujetoActivoController`, `IvaManagementController`) fueron
  **eliminados** (#3c); `FinancialStatementsController` usa MediatR con
  handlers honestos `NotImplemented`. `AccountingExportController` âœ… (#4);
  `RecargoController` en #3b; `ViesController` en #5.
- Treasury: ninguno pendiente (5/5 vÃ­a `IMediator`; ver backlog #9).
- Payroll: `PayrollController` âœ… delgado â€” solo `IMediator` (#10).
- Billing: todos los controllers del mÃ³dulo usan `IMediator` (FacturaE vÃ­a
  `GenerateFacturaEQuery`; API pÃºblica vÃ­a `GetPublicInvoicesQuery`).
- Inventory: todos los controllers del mÃ³dulo usan `IMediator`.

Ejemplo concreto de gravedad: `TreasuryController.cs` implementa un parser
CSV de extractos bancarios completo, con construcciÃ³n de entidades EF y
`SaveChangesAsync`, directamente en la acciÃ³n del controller (no en un
handler). `RecargoController.cs` calcula agrupaciones de recargo de
equivalencia y mapeo a casillas del Modelo 303 igual de inline.

Por contraste, `ClientsController.cs` (Crm) y `StockController.cs`
(Inventory) son controllers delgados: solo construyen el Command/Query y
llaman a `_mediator.Send(...)`. El patrÃ³n correcto existe en el cÃ³digo base,
pero no se aplicÃ³ de forma consistente â€” es el estÃ¡ndar a exigir en todo ADR
nuevo (ver checklist en `0000-template.md`).

**Corregido â€” validaciÃ³n silenciosamente muerta:** `Erp.Application/DependencyInjection.cs`
registra `ValidationBehavior` (pipeline de MediatR) pero solo escaneaba
validators del propio assembly `Erp.Application` (`AddValidatorsFromAssembly(Assembly.GetExecutingAssembly())`).
NingÃºn `Modules/*/Infrastructure/DependencyInjection.cs` registraba sus
propios validators de FluentValidation. Revisando los 9 mÃ³dulos, el Ãºnico
validator de FluentValidation real (`AbstractValidator<T>`) que existÃ­a fuera
del core era `Modules/Crm/Application/Features/Crm/Validators/CreateClientValidator.cs`
â€” existÃ­a en el cÃ³digo pero **nunca se ejecutaba en runtime**
(`IValidator<CreateClientCommand>` nunca se resolvÃ­a). Se corrigiÃ³ aÃ±adiendo
`AddValidatorsFromAssembly` a `AddCrmInfrastructure`. El resto de mÃ³dulos no
tiene validators de FluentValidation (no hay nada mÃ¡s que registrar); si se
aÃ±ade uno nuevo en cualquier mÃ³dulo, recordar registrarlo en el
`DependencyInjection.cs` de ese mÃ³dulo o seguirÃ¡ sin ejecutarse.

### 6. Escalabilidad
- **Corregido** â€” N+1 en `Modules/Purchasing/Application/Features/Receipts/Handlers/CreateGoodsReceiptHandler.cs`
  y `Modules/Sales/Application/Features/Deliveries/Handlers/CreateDeliveryNoteHandler.cs`:
  ambos hacÃ­an `await _context.Xxx.FindAsync(...)` dentro de un `foreach` por
  lÃ­nea del documento; ahora cargan todas las lÃ­neas necesarias en una Ãºnica
  consulta batch (`Where(...).ToDictionaryAsync(...)`) antes del bucle.
- **PaginaciÃ³n ausente** en la mayorÃ­a de `Get*Query` de CRM, Accounting,
  Treasury, Billing e Inventory (listas sin `Skip`/`Take`). Sales sÃ­ pagina
  bien (`GetAllCustomerInvoicesQuery`, `GetDeliveryNotesQueries` devuelven un
  `Paginated...Result`) e Inventory soporta `page`/`pageSize` +
  `X-Total-Count` en `StockController.GetMovements` â€” es el patrÃ³n a copiar
  en el resto de mÃ³dulos.
- `EcbExchangeRateProvider.cs` (Treasury) hardcodea la URL del BCE como
  `const string` en vez de `IConfiguration` â€” menor, pero real.
- **AÃ±adir un mÃ³dulo 10 no es autodescubrible**: exige tocar
  `backend/Erp.Api/Program.cs` manualmente en cuatro sitios (`AddApplicationPart`,
  `Add<MÃ³dulo>Infrastructure`, un bloque `AddMediatR` con un tipo de handler
  como "ancla" de assembly, y el propio `DependencyInjection.cs` del mÃ³dulo).
  No hay nada que fuerce a no olvidar un paso â€” Payroll es la prueba: le
  falta el bloque `AddMediatR` (no tiene handlers) y nadie lo detectÃ³ hasta
  esta auditorÃ­a.

### 7. CQRS
Donde se usa, estÃ¡ bien aplicado: no se encontraron queries con efectos
secundarios ocultos ni commands mal nombrados como getters. Pero:
- **Payroll no tiene CQRS en absoluto.** `Modules/Payroll/Application/`
  contiene Ãºnicamente `Interfaces/IPayrollDbContext.cs` â€” no hay `Features`,
  `Commands`, `Queries` ni `Handlers`. `PayrollController.cs` inyecta el
  DbContext y hasta `AccountingService` (de otro mÃ³dulo) directamente y
  ejecuta ahÃ­ toda la lÃ³gica de altas, liquidaciones y exportaciÃ³n TC1/TC2/RED.
  `Program.cs` no registra ningÃºn `AddMediatR` para Payroll, a diferencia de
  los otros 8 mÃ³dulos.
- El bypass de MediatR en 25 controllers (punto 5) es, visto desde CQRS, el
  mismo problema: esos endpoints no pasan por el pipeline de comandos/queries
  en absoluto, asÃ­ que tampoco se benefician de `ValidationBehavior` ni de
  ningÃºn pipeline behavior futuro (logging, autorizaciÃ³n declarativa, etc.).

## RelaciÃ³n con otros mÃ³dulos
- **ADR-0001** (arquitectura general): la violaciÃ³n de direcciÃ³n de
  dependencias (`Erp.Infrastructure` â†’ 5 mÃ³dulos) y el patrÃ³n de "ancla de
  assembly" para `AddMediatR` son extensiones directas de lo ya descrito ahÃ­.
- **ADR-0004** (CRM): `ClientHandlers.cs` como cÃ³digo muerto/ancla de
  assembly (corregido).
- **ADR-0006** (Accounting): `AccountingExportController` (SRP), duplicado
  VatController/CalculateVatCommand (corregido), `IAccountingDbContext` de 29 DbSets
  (ISP), controllers sin MediatR.
- **ADR-0008** (Inventory): entidades nÃºcleo fuera de `Modules/Inventory/Domain`.
- **ADR-0009** (Payroll): ausencia total de CQRS.
- **ADR-0010 / ADR-0011** (Purchasing/Sales): un solo assembly, sin frontera
  de capas real; N+1 en ambos mÃ³dulos (corregido).
- **ADR-0012** (Treasury): los 5 controllers sin MediatR, acoplamiento a
  Accounting.

## Buenas prÃ¡cticas aplicables
Este es el checklist que queda incorporado a `0000-template.md` para todo ADR
futuro â€” antes de dar por cerrado un mÃ³dulo o una implementaciÃ³n, verificar:
1. **SOLID**: Â¿alguna clase concentra mÃ¡s de una razÃ³n de cambio (SRP)? Â¿hay
   reglas de negocio que cambian con frecuencia codificadas como switch/dict
   en vez de datos (OCP)? Â¿alguna interfaz obliga a implementar/depender de
   miembros que el consumidor no usa (ISP)?
2. **Clean Architecture**: Â¿las referencias de proyecto solo fluyen hacia
   adentro (Apiâ†’Applicationâ†’Domain, Infrastructureâ†’Application/Domain)? Â¿el
   Domain estÃ¡ libre de EF Core/ASP.NET? Â¿el core depende de los mÃ³dulos o al
   revÃ©s?
3. **Estructura de carpetas**: Â¿el mÃ³dulo tiene 4 `.csproj` separados
   (Api/Application/Domain/Infrastructure) con frontera real de compilador?
4. **DuplicaciÃ³n**: Â¿existe la misma lÃ³gica de negocio implementada dos veces
   (controller vs. handler, o entre dos mÃ³dulos)?
5. **CÃ³digo limpio â€” controllers delgados**: Â¿el controller solo construye
   un Command/Query y llama a `_mediator.Send(...)`, o tiene lÃ³gica de
   negocio, acceso a datos o construcciÃ³n de entidades inline?
6. **Escalabilidad**: Â¿los endpoints de listado paginan? Â¿hay queries dentro
   de bucles (N+1)? Â¿hay configuraciÃ³n/URLs hardcodeadas que deberÃ­an venir
   de `IConfiguration`?
7. **CQRS**: Â¿el flujo pasa por `IMediator` con Command/Query separados, o el
   controller bypasea el pipeline? Â¿el mÃ³dulo tiene sus propios validators de
   FluentValidation registrados en su `DependencyInjection.cs` (no basta con
   que existan como clase â€” hay que confirmar el `AddValidatorsFromAssembly`)?

## Consecuencias
- La deuda tÃ©cnica mÃ¡s seria por impacto es la de direcciÃ³n de dependencias
  (`Erp.Infrastructure` â†’ 5 mÃ³dulos) y las entidades nÃºcleo de Inventory
  fuera de su mÃ³dulo: ambas comprometen la premisa central de "monolito
  modular" (mÃ³dulos razonablemente independientes) mÃ¡s que cualquier
  problema de un controller aislado.
- El hallazgo de mayor volumen histÃ³rico fue el bypass de CQRS en controllers
  (âœ… cerrado: 43/43 con `IMediator`; los stubs mock de Accounting fueron
  eliminados en #3c). Cualquier trabajo futuro en Accounting debe implementar
  lÃ³gica fiscal real en handlers, no reintroducir controllers con datos fijos.
- La validaciÃ³n de FluentValidation "muerta" era un riesgo silencioso: el
  cÃ³digo daba la falsa sensaciÃ³n de estar validado (el validator existÃ­a,
  compilaba) pero no se ejecutaba en producciÃ³n (corregido para CRM; ver
  Ã­tem 6 del backlog).
- Ninguno de estos hallazgos es bloqueante para seguir desarrollando, pero
  todos incrementan el costo de cualquier cambio futuro que toque esas zonas
  â€” de ahÃ­ que este ADR quede referenciado desde el template como checklist
  obligatorio, no solo como informe puntual.

## Hallazgos crÃ­ticos â€” cÃ³digo real pero incorrecto frente a especificaciÃ³n externa

CategorÃ­a distinta del catÃ¡logo de mock de mÃ¡s abajo: esto **no es cÃ³digo
que finge funcionar** (no hay ningÃºn `Ok(new {...})` hardcodeado) â€” es
cÃ³digo real que llama de verdad a la AEAT/Stripe/un banco, firma
digitalmente, calcula hashes. La auditorÃ­a de esta secciÃ³n responde a una
pregunta que ninguna auditorÃ­a anterior de este ADR se habÃ­a hecho: **el
cÃ³digo que SÃ es real, Â¿es ademÃ¡s correcto frente a la especificaciÃ³n
externa exacta que dice implementar?** Cada fila estÃ¡ verificada leyendo el
archivo citado, no es una sospecha. Detalle completo por mÃ³dulo en
ADR-0013 (VeriFactu/SII/FacturaE), ADR-0012 (SEPA) y ADR-0014 (Stripe).

| # | Hallazgo | MÃ³dulo | Prioridad |
|---|---|---|---|
| 0a | **VeriFactu** â€” ~~huella/XML/anulaciones/auditorÃ­a/leyenda~~ âœ… **Cerrado** â€” modo no-VERI*FACTU: `Verifactu:SubmissionMode=LocalOnly` (sin envÃ­o TIKE, sin QR, leyenda PDF alternativa, `VerifactuRealtimeSubmission` en factura, `TipoUsoPosibleSoloVerifactu=N`) | Billing/Core | âœ… Corregido |
| 0b | **SII** â€” **ðŸŸ¡ Parcial+** â€” namespaces duales + XAdES; homologaciÃ³n offline `GET /api/sii/validate` + `SiiXmlStructureValidator`; envÃ­o HTTP real con `Sii:SendEnabled=true` (desactivado por defecto). `GET /api/fiscal/homologation/status` documenta bloqueo externo. Pendiente: homologaciÃ³n AEAT en entorno test | Core | **CrÃ­tica â€” bloqueado externo** |
| 0c | **FacturaE** â€” **ðŸŸ¡ Parcial+** â€” firma XAdES real ya presente (antes `.xsig` sin firmar), NIF validado, direcciones ya no hardcodeadas, `POST .../submit-face`. ~~namespace raÃ­z incorrecto (`Version3.2.2/Facturae32.xsd`); `FacturaEXmlStructureValidator` validaba contra esa misma constante equivocada (circular)~~ **âœ… Corregido**: namespace real (`Versiones/Facturaev3_2_2.xml`) unificado en `FacturaEXmlStructureValidator.FacturaENamespace`, y `FacturaEService.cs` ahora referencia esa misma constante (una sola fuente de verdad, ya no puede volver a divergir). Verificado con build+test completo. Pendiente: bloque `Extensions` sigue con estructura inventada; perfil de firma es XAdES-BES, FACe exige XAdES-EPES con `SignaturePolicyIdentifier`; homologaciÃ³n entorno test | Billing | Alta â€” pendiente perfil de firma y homologaciÃ³n, ya no es "namespace incorrecto" |
| 0d | **SEPA** â€” **ðŸŸ¡ Parcial+** â€” endpoints pain.001/pain.008 + `SepaXmlStructureValidator` offline. `GET /api/fiscal/homologation/status` marca `bankHomologation=false`. Pendiente: homologaciÃ³n bancaria | Treasury | Alta â€” bloqueado externo |
| 0e | **Stripe â€” idempotencia webhook + claves test/live** | Core (StripeService) | âœ… Corregido â€” `StripeWebhookEvents` + skip por `EventId`; `StripeOptionsValidator` (`IValidateOptions`) exige `sk_test_` fuera de Production y `sk_live_` en Production (+ prefijo `pk_*` si `PublishableKey` estÃ¡ definido); 3 tests unitarios |
| 0f | **Sin validaciÃ³n de NIF/CIF/NIE** | CRM/Billing/Core | ~~el CIF tenÃ­a la lÃ³gica de letra/dÃ­gito de control invertida (A/B/E/H recibÃ­an letra en vez de dÃ­gito, P/Q/S recibÃ­an dÃ­gito en vez de letra); el CIF real de Banco Santander `A39000013` era rechazado~~ **âœ… Corregido** â€” ramas de `ValidateCif` corregidas (A/B/E/H â†’ dÃ­gito; P/Q/S â†’ letra; resto acepta cualquiera de los dos, que es la regla real para esos prefijos). Verificado de nuevo con cÃ¡lculo manual: `A39000013` y `A28015865` (TelefÃ³nica) ya se aceptan. El test `FindValidCif` (que generaba su propio caso por fuerza bruta contra el validador roto, dando falsa confianza) se sustituyÃ³ por casos con CIFs reales conocidos + casos de rechazo con dÃ­gito de control alterado. Se eliminÃ³ tambiÃ©n `tmp-find-cif.cs`, un resto de depuraciÃ³n commiteado por error. NIF/NIE seguÃ­an y siguen bien |
| 0g | **Ninguna empresa registrada por `/register`/`/signup` tenÃ­a plan contable â€” cualquier cobro de factura fallaba** | Core (Auth) â†” Accounting | ~~`PgcSeeder.cs` (`Modules/Accounting/Infrastructure/Seeding/`) sembraba el PGC pero no lo invocaba nadie en todo el repo (verificado: 0 referencias); el Ãºnico seeding real vivÃ­a inline en `Program.cs`, dentro de un bloque `if (!dbContext.Users.Any())` â€” solo se ejecuta la primera vez que la BBDD estÃ¡ vacÃ­a, para la empresa de desarrollo. Cualquier empresa dada de alta despuÃ©s (flujo normal de negocio) quedaba sin ninguna cuenta, y `PaymentReceivedEventHandler` lanza `InvalidOperationException` al buscar "570"/"572"/"430" por cÃ³digo exacto â€” el cobro de cualquier factura de cualquier empresa real fallaba. AdemÃ¡s `PgcSeeder.cs` tenÃ­a bugs propios: cÃ³digos de cuenta duplicados (`"210"` y `"110"` cada uno tres/dos veces), `Type` en espaÃ±ol (no usado por ningÃºn filtro) y encoding roto (mojibake)~~ **âœ… Corregido** â€” nuevo `CompanyCreatedEvent` (publicado por `RegisterCompanyHandler` y `AddCompanyFromAccountHandler` tras crear la empresa) consumido por `SeedChartOfAccountsHandler` (`Modules/Accounting/Application/Handlers/`, reemplaza y borra el `PgcSeeder.cs` muerto), con la lista de cuentas corregida (Types en inglÃ©s consistentes con `GetBalanceSheetQuery`/`GetProfitAndLossQuery`/`GetMayorQuery`, sin cÃ³digos duplicados). El bloque inline de `Program.cs` tambiÃ©n se sustituyÃ³ por el mismo evento â€” una Ãºnica fuente de verdad, idempotente. 7 tests nuevos (`SeedChartOfAccountsHandlerTests`, mÃ¡s aserciones en `RegisterCompanyHandlerTests`/`AddCompanyFromAccountHandlerTests` verificando que el evento se publica) |

**Por quÃ© esto es mÃ¡s grave que el resto del backlog de cÃ³digo**: los Ã­tems
1-19 son deuda tÃ©cnica que hace el cÃ³digo mÃ¡s difÃ­cil de mantener, pero
funciona. Los Ã­tems 0a-0f son al revÃ©s â€” el cÃ³digo funciona (no lanza
excepciones, no requiere arreglar nada para "que compile"), pero el
resultado que produce serÃ­a rechazado por un tercero externo (AEAT, un
banco) si se usara en producciÃ³n real. Es el tipo de deuda mÃ¡s peligroso
porque no se detecta con ningÃºn build verde ni con pruebas manuales
superficiales â€” solo se descubre al intentar el envÃ­o real u homologar el
software, momento en el que ya hay facturas/pagos reales de por medio.

## PrÃ³ximos pasos
Backlog de remediaciÃ³n, priorizado de menor a mayor riesgo/alcance. Cada
Ã­tem se corrige de forma aislada y verificada (build/CI en verde) antes de
pasar al siguiente â€” nada de refactors masivos de una vez, este es software
fiscal sin tests automatizados. Estado se actualiza en este mismo ADR a
medida que se completa cada uno.

| # | Hallazgo | MÃ³dulo | Estado |
|---|---|---|---|
| 1 | Duplicado VatController/CalculateVatCommand | Accounting | âœ… Corregido |
| 2 | `ClientHandlers.cs` cÃ³digo muerto usado como ancla de assembly de MediatR | Crm | âœ… Corregido |
| 3a | Duplicado ProrrataController/CalculateProrrataCommand (+ bug real: contrato no coincidÃ­a con el frontend) | Accounting | âœ… Corregido |
| 3b | `RecargoController` sin `IMediator` (tiene lÃ³gica real: queries a `IBillingDbContext`, mapeo Modelo 303 â€” migraciÃ³n no trivial) | Accounting | âœ… Corregido |
| 3c | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `AgingController` sin `IMediator` | Accounting | âœ… Corregido â€” eliminados jul 2026; modelos 303/347 reales; `FinancialStatementsController` vÃ­a MediatR (#26); `GET /api/accounting/aging` real (jul 2026) |
| 4 | `AccountingExportController` (SRP, ~350 lÃ­neas tras extracciÃ³n) â€” el motivo estructural del acoplamiento cross-mÃ³dulo estÃ¡ en el Ã­tem 19b | Accounting | âœ… Corregido â€” 16/16 rutas vÃ­a `IMediator`; controller delgado; `modelo347-aeat-txt` en `ExportModelo347AeatTxtQuery` + `IModelo347Exporter.ExportAeatTxtAsync` |
| 5 | `ViesController` (Accounting) sigue duplicando lo que ya resuelve `Erp.Api/TaxController` | Accounting | âœ… Corregido |
| 6 | Validators de FluentValidation nunca registrados por mÃ³dulo (`AddValidatorsFromAssembly` ausente) | Crm | âœ… Corregido |
| 7 | N+1 en `CreateGoodsReceiptHandler` / `CreateDeliveryNoteHandler` | Purchasing / Sales | âœ… Corregido |
| 8 | PaginaciÃ³n ausente en `Get*Query` (CRM, Accounting, Treasury, Billing, Inventory) | Varios | âœ… Corregido â€” CRM, Billing, Inventory, Accounting (`GetJournalQuery`) y Treasury (`GetBankMovementsQuery`, `GetCashEffectsQuery`, `GetPaymentOrdersQuery`) paginados con `X-Total-Count` |
| 9 | Los 5 controllers de Treasury sin `IMediator` (incluye parser CSV inline) | Treasury | âœ… Corregido â€” los 5 controllers del mÃ³dulo delegan en handlers (`TreasureHandlers`, `GuaranteeHandlers`, `FinancingHandlers`, `ConsolidationHandlers`) |
| 10 | Payroll sin capa CQRS/MediatR | Payroll | âœ… Corregido â€” `PayrollController` solo `IMediator`; `Features/{Employees,Settlements,Exports}/`; exportes TC1/TC2/RED en `PayrollExportHandlers.cs` |
| 11 | `PurchaseOrdersController` / `SalesOrdersController` sin `IMediator` | Purchasing / Sales | âœ… Corregido â€” handlers en `Application/Features/Orders/`; controllers delgados; filtro por tenant en queries |
| 12 | Sales y Purchasing: frontera de capas por compilador | Purchasing / Sales | âœ… â€” 4 proyectos por mÃ³dulo; **todos los mÃ³dulos** en `Erp.slnx` (Accounting, Expenses, Treasury, Payroll aÃ±adidos jul 2026) |
| 13 | `Erp.Infrastructure` depende de 5 mÃ³dulos (direcciÃ³n invertida) | Core | âœ… Corregido â€” sin refs a `*.Application` de mÃ³dulos; SII/automation vÃ­a puertos (`ISiiEmitidasInvoiceSource`, `ISiiRecibidasExpenseSource`, `IAutomationBillingQuery`, `IAutomationInventoryQuery`); OCR â†’ `Expenses.Infrastructure`; Verifactu XML â†’ `Billing.Infrastructure`; `IExpenseOcrCrmBridge` en CRM. `ErpDbContext` solo referencia `*.Domain` para `Ignore&lt;T&gt;()` |
| 14 | Entidades nÃºcleo fuera de su mÃ³dulo (Inventory + Accounting 6 entidades en `Erp.Domain`) | Inventory, Accounting | âœ… Corregido â€” todas las entidades Accounting migradas a `Accounting.Domain`; Inventory (`Product`, `Warehouse`, `Stock`, `StockMovement`) fÃ­sicamente en `Modules/Inventory/Domain/Entities` |
| 15 | `ConsolidationController.ConsolidateGroup` responde Ã©xito sin consolidar nada; `ConsolidatedFinancialStatements` nunca se escribe | Treasury | âœ… Corregido â€” `ConsolidateGroupHandler` agrega P&amp;L y balance desde `JournalEntryLine` (PGC 6/7/1-5) por filial + matriz, ponderado por `OwnershipPercentage`; persiste en `ConsolidatedFinancialStatements` |
| 16 | `ExchangeRateRefreshJob` no hace nada nunca: corre fuera de contexto HTTP y `TenantContext.TenantId` siempre es `null` ahÃ­, sin log ni error | Treasury | âœ… Corregido â€” `RefreshAllTenantsRatesAsync` itera empresas con `IgnoreQueryFilters`; el job llama a ese mÃ©todo; log cuando no hay divisas activas |
| 17 | `frontend/billing/facturae/page.tsx` â€” pÃ¡gina 100% mock, botones sin `onClick`, mientras el backend (`FacturaEController`) sÃ­ es real | Billing (frontend) | âœ… Corregido |
| 18 | `frontend/accounting/iva-registers/page.tsx` â€” pÃ¡gina 100% mock con cifras inconsistentes entre cabecera y tabla | Accounting (frontend) | âœ… Corregido â€” resumen desde `/api/proxy/invoices` y `/api/proxy/expenses/documents`; descarga CSV real vÃ­a `libro-iva-emitidas`/`libro-iva-recibidas`; enlace a `/sii` para envÃ­o |
| 19 | `OutboxMessageProcessorJob.cs` â€” implementaciÃ³n completa y correcta del procesador de Outbox, pero huÃ©rfana: nunca se registra, existe un duplicado distinto que sÃ­ corre (`OutboxProcessorJob.cs`) | Core | âœ… Corregido â€” eliminado `OutboxMessageProcessorJob.cs`; `OutboxProcessorJob` (registrado en `Program.cs`) es la Ãºnica implementaciÃ³n activa |
| 19a | **Dependencia circular real entre dos mÃ³dulos**: ~~`Modules/Crm/Api/Erp.Modules.Crm.Api.csproj:15` referencia `Modules/Expenses/Application`, y a la vez `Modules/Expenses/Api/Erp.Modules.Expenses.Api.csproj:15` referencia `Modules/Crm/Application`~~ **âœ… Corregido** â€” eliminada `ProjectReference` Crm.Apiâ†’Expenses.Application; gastos por proveedor vÃ­a `GET /api/expenses/by-supplier/{id}`; ficha proveedor (`suppliers/page.tsx`) hace fetch paralelo. `Expenses.Api` ya no referencia `Crm.Application` (vÃ©ase #19d) | Crm â†” Expenses | âœ… Corregido |
| 19b | `Accounting.Api` referencias cruzadas a otros mÃ³dulos | Accounting | âœ… Corregido â€” sin refs a Billing/Crm/Expenses/Payroll ni `Erp.Infrastructure`; lectura cross-mÃ³dulo solo en `Accounting.Infrastructure` (exporters) |
| 19c | Acoplamiento Treasury/Payroll â†’ Accounting sin abstracciÃ³n | Treasury, Payroll, Accounting | âœ… Corregido â€” `IBankReconciliationLedgerQuery`, `IPayrollJournalEntryGenerator`, `IConsolidationMetricsQuery` en `Erp.Application`; `Treasury.Application` sin ref a `Accounting.Application`; `ConsolidateGroupHandler` usa puerto de mÃ©tricas |
| 19d | `ExpensesController` inyecta `ICrmDbContext` y escribe directamente en `_crmCtx.ActivityLogs` (`Modules/Expenses/Api/Controllers/ExpensesController.cs:3,23,31,72`) â€” un mÃ³dulo de negocio escribiendo en la tabla de otro sin pasar por su API/eventos. Contraste positivo ya existente en el propio cÃ³digo: los `EventHandlers` de Inventory y Crm (`ExpenseApprovedInventoryHandler`, `QuoteAcceptedCrmHandler`, etc.) sÃ­ reaccionan a eventos de MediatR sin inyectar el DbContext ajeno â€” ese es el patrÃ³n a copiar aquÃ­ | Expenses, Crm | âœ… Corregido â€” upload QR publica `ExpenseUploadCreatedEvent`; `ExpenseUploadActivityHandler` (CRM) escribe `ActivityLog`; eliminada `ProjectReference` Crm desde `Expenses.Api` |
| 19e | Self-registro modular `IErpModule` + `ErpModuleExtensions` | Todos (Core) | âœ… Corregido â€” los **9 mÃ³dulos** (Accounting, Billing, CRM, Treasury, Purchasing, Inventory, Expenses, Payroll, Sales) vÃ­a `*ErpModule`; `Program.cs` solo registra `AddErpModule` + `AddErpModuleControllers` por mÃ³dulo |
| 19f | Inventory usa `API` en vez de `Api` no solo como nombre de carpeta sino en el namespace real compilado: `namespace Erp.Modules.Inventory.API.Controllers` en sus 5 controllers, referenciado explÃ­citamente en `Program.cs:56` (`typeof(Erp.Modules.Inventory.API.Controllers.ProductsController)`) â€” inconsistente con los otros 8 mÃ³dulos (`Erp.Modules.<Nombre>.Api.Controllers`) | Inventory | âœ… Corregido jul 2026 â€” namespace unificado `Erp.Modules.Inventory.Api` |
| 19g | CosmÃ©tico, agrupado: carpeta de migraciones de Purchasing en `Modules/Purchasing/Migrations/` en vez de `Modules/Purchasing/Infrastructure/Migrations/` (como los otros 8 mÃ³dulos); `Modules/Sales/Infrastructure/DependencyInjection.cs` usa namespace de bloque (`namespace X { }`) en vez de namespace de archivo (`namespace X;`) como el resto del cÃ³digo | Purchasing, Sales | âœ… Corregido jul 2026 â€” Purchasing migrations + Sales DI file-scoped |

**Verificaciones limpias de esta auditorÃ­a de modularidad (sin hallazgos, se deja constancia explÃ­cita para no repetir el anÃ¡lisis):** ningÃºn mÃ³dulo con proyectos separados (los 7 que no son Sales/Purchasing) tiene su Domain o Application referenciando su propia Infrastructure; no se encontrÃ³ ninguna otra entidad de negocio (Billing, Crm, Expenses, Payroll, Purchasing, Sales, Treasury) mal ubicada en `Erp.Domain` (Ã­tem 14 âœ…); el frontend (`frontend/src/app/`) estÃ¡ limpiamente separado por mÃ³dulo en carpetas, sin ningÃºn import cruzado entre carpetas de mÃ³dulos distintos.

**Ãtems 20+: mejoras funcionales (nivel producto).** A diferencia de 1-19
(deuda de cÃ³digo: duplicaciÃ³n, CQRS, estructura), estos son huecos
funcionales â€” el ERP hace CRUD correcto por mÃ³dulo pero no los conecta como
procesos de negocio reales, o tiene funcionalidad fiscal/financiera
simulada en vez de real. Alcance y esfuerzo mayores; no son "arreglar algo
roto" sino "construir la integraciÃ³n/lÃ³gica que falta". Priorizados por
relaciÃ³n esfuerzo/impacto (los primeros reutilizan cÃ³digo que ya existe).

| # | Mejora | MÃ³dulos | Prioridad |
|---|---|---|---|
| 20 | `GoodsReceipt` (Purchasing) no incrementa stock en Inventory al recibir mercancÃ­a â€” sin esto, "recepciÃ³n de compra" no tiene efecto real en el almacÃ©n | Purchasing â†’ Inventory | âœ… Corregido â€” `CreateGoodsReceiptHandler` publica `GoodsReceiptCreatedEvent`; `GoodsReceiptInventoryHandler` incrementa stock |
| 21 | `DeliveryNote` (Sales) no decrementa stock en Inventory al entregar â€” sin esto, "entrega de pedido" no descuenta existencias | Sales â†’ Inventory | âœ… Corregido â€” `CreateDeliveryNoteHandler` publica `DeliveryNoteCreatedEvent`; `DeliveryNoteInventoryHandler` decrementa stock |
| 22 | `Sales.CustomerInvoice` y `Billing.Invoice` | Sales â†” Billing | âœ… Corregido â€” lÃ­neas operativas sin fiscal; totales header (`SubTotal`/`TaxAmount`/`Total`) documentados como cache desnormalizado desde Billing al crear |
| 23 | `SalesOrder.ClientId`/`ClientName` es texto suelto sin FK real a `Crm.Client` â€” Sales no estÃ¡ realmente integrado con CRM | Sales â†” Crm | âœ… Corregido â€” `CreateSalesOrderHandler` valida `ClientId` vÃ­a `IClientInfoService` y fija `ClientName` desde CRM; migraciÃ³n `FK_SalesOrders_Clients_ClientId`; frontend `sales/orders/new` envÃ­a `clientId` |
| 24 | Implementar Modelo 303 (IVA trimestral) real a partir de facturas bloqueadas + `JournalEntry` (472 deducible) | Accounting | âœ… Corregido â€” `Modelo303Reader`, `GET /api/accounting/modelo-303`, export CSV/XML; frontend `accounting/aeat` conectado |
| 25 | Implementar Modelo 347 (operaciones anuales >3.005,06â‚¬) real, sustituyendo el stub | Accounting | âœ… Corregido â€” `Modelo347Reader` + `GetModelo347JsonQuery`; export CSV/TXT; frontend `accounting/aeat` con consulta preview y descargas |
| 26 | Estados financieros reales desde `JournalEntry`/`JournalEntryLine` | Accounting | âœ… Corregido â€” PyG/balance vÃ­a `ReportsController` (`GetProfitAndLossQuery`, `GetBalanceSheetQuery`); EFE/patrimonio en `FinancialStatementsController`; frontend `accounting/reports` con errores visibles |
| 27 | Activar el motor de automatizaciÃ³n: registrar `RuleEvaluatorJob` en Hangfire, hacer que `CreateRuleCommand` persista, conectar `settings/automation` al backend â€” la lÃ³gica de negocio (facturas vencidas, stock bajo) ya existe, solo falta cablearla | Core/AutomatizaciÃ³n | âœ… Corregido â€” job diario 9:00; API + frontend; `DatabaseRuleEvaluator` evalÃºa reglas activas de BD (`OnInvoiceCreated`, `OnInvoiceOverdue`, `OnStockBelowReorder`) con condiciones y acciÃ³n `SendEmail`; **tiempo real** vÃ­a `RealtimeRuleEvaluator` + handlers `OnLeadStatusChanged`/`OnExpenseApproved` |
| 28 | Sustituir la importación manual de CSV bancario por integración de banca abierta (PSD2) para conciliación en tiempo real | Treasury | ✅ Corregido — `IOpenBankingProvider`, `MockOpenBankingProvider`/`StubOpenBankingProvider`, `SyncOpenBankingHandler`, `POST /api/treasury/bank-accounts/{id}/sync-open-banking`, config `OpenBanking:*` en appsettings; tests unit + `Phase18IntegrationHttpTests` |
| 29 | Payroll: integración real con Sistema RED/Seguridad Social, más allá de los exports TC1/TC2 "orientativos" actuales | Payroll | ✅ Corregido (alcance máximo sin homologación) — `RedSiltraFileBuilder` (registros 01/02/99, 250 chars, ISO-8859-1), `SpanishSocialSecurityNumberValidator` (NAF/CCC módulo 97), `PayrollRedExportValidator`, `GET /api/payroll/export/red`; TC1/TC2 con metadatos empresa/CCC; frontend botón RED; golden tests + `docs/payroll-red-siltra.md`; **no homologado TGSS** (disclaimer obligatorio) |
| 30 | Flujos de aprobación (pedidos de compra o gastos por encima de un umbral, antes de confirmar/contabilizar) | Purchasing/Expenses | ✅ Corregido — `Company.ApprovalThresholdAmount`, estados PO Draft→PendingApproval→Approved/Rejected, handlers MediatR + `PurchaseOrder:Approve`, gastos `submit-for-approval` con umbral, frontend badges/botones en `/purchasing/orders` |
| 31 | Conectar el interceptor de Audit Log (`AuditInterceptor.cs`, ya documentado como cÃ³digo muerto en el catÃ¡logo de mock de arriba) â€” de cara al usuario el sistema aparenta tener auditorÃ­a inmutable y hoy no la tiene | Core | âœ… Corregido â€” `AuditSaveChangesInterceptor` en ErpDbContext + 9 mÃ³dulos; hash SHA256; `settings/audit-logs` consulta datos reales |

**Ãtems 32+: mejoras adicionales de plataforma y producto**, identificadas al
cierre de esta auditorÃ­a (no se derivan de un hallazgo puntual del cÃ³digo,
sino de comparar el estado actual contra lo que exigirÃ­a un ERP de clase
mundial). Mezcla cÃ³digo/plataforma (32-37) y producto (38-42).

| # | Mejora | MÃ³dulos | Prioridad |
|---|---|---|---|
| 32 | Cero tests automatizados en todo el repo â€” priorizar tests de integraciÃ³n sobre los flujos crÃ­ticos (facturaciÃ³n, asientos automÃ¡ticos, aislamiento multi-tenant) antes que cobertura exhaustiva | Todos | ðŸŸ¡ Ampliado (fase 4) â€” **~266 tests** (133 unit + 81 integraciÃ³n + 4 arquitectura + 48 frontend Vitest); ValidationBehavior + validators; Accounting export 303/347; Platform/Subscriptions/ApiKeys; lock factura â†’ asiento (integraciÃ³n); fiscal smoke JWT; coverlet **20%**; Playwright loginâ†’clientes seed docker; plan en `docs/testing-strategy.md` |
| 33 | No existe middleware global de manejo de excepciones â€” cualquier excepciÃ³n no controlada (incluida la `ValidationException` de FluentValidation reciÃ©n activada en CRM) se filtra como un 500 crudo sin `ProblemDetails` ni contrato de error consistente | Core | âœ… Corregido â€” `ExceptionHandlingMiddleware` devuelve `application/problem+json` (400/401/404/500 segÃºn tipo) |
| 34 | Aislamiento multi-tenant a un solo nivel de defensa (global query filters de EF Core); aÃ±adir Row-Level Security de Postgres como segunda barrera | Core | ✅ Ampliado — **19 tablas** vía `PostgresRlsBootstrap` (10 core + 9 módulo: Leads, Quotes, ExpenseDocuments, SalesOrders, PurchaseOrders, …). Test integración `PostgresRlsBootstrapTests`. Fallback prod `Postgres__RlsEnabled:-true` |
| 35 | Ninguna de las 19 violaciones de arquitectura de este ADR se detecta automÃ¡ticamente en CI; aÃ±adir tests de arquitectura (tipo NetArchTest: "ningÃºn controller referencia DbContext directamente", "Domain no depende de Infrastructure") para que las reglas se apliquen solas en cada PR | Core/CI | âœ… Ampliado â€” 3 reglas; solo exempt `FiscalHomologationController` y `StripeWebhookController` |
| 36 | Sin observabilidad real: no hay logging estructurado, tracing distribuido ni mÃ©tricas en ningÃºn mÃ³dulo â€” depurar producciÃ³n (p. ej. por quÃ© se atascÃ³ el outbox) hoy depende de logs de consola sueltos | Core | âœ… Corregido â€” Serilog + OpenTelemetry (mÃ©tricas Prometheus `/metrics`, tracing OTLP opcional, collector en compose local) |
| 37 | Frontend con muy poca reutilizaciÃ³n de componentes â€” ver auditorÃ­a dedicada y desglose en Ã­tems 43-51 | Frontend | âœ… Corregido â€” `FormErrorBanner`, `PageListLayout`, `FormLabel`, `EmptyState`, `LoadingPlaceholder` |
| 38 | Multi-moneda real en Billing (facturar en divisa distinta del euro con conversiÃ³n automÃ¡tica usando los tipos de cambio de Treasury) â€” hoy no estÃ¡ claro que Billing soporte esto | Billing â†” Treasury | Media |
| 39 | ~~Portal de autoservicio para cliente/proveedor (ver y pagar facturas, subir facturas de proveedor) â€” hoy todo el flujo es interno, sin reenvÃ­o manual de PDFs~~ **âœ… Corregido â€” las 3 piezas (ver factura, pagar factura, subir factura de proveedor)**: `Invoice.PublicViewToken` (mismo patrÃ³n que `Quote.AcceptanceToken`), `GetInvoiceByTokenQuery`/`GetInvoiceByTokenHandler` (Billing.Application) e `InvoicePublicDto` reutilizando `PublicInvoiceLineDto`. Nuevo `PublicInvoiceViewController` (`api/v1/public/invoice-view/{token}`, `[AllowAnonymous]`) â€” no confundir con `PublicInvoicesController` (`api/v1/public/invoices`), que es un endpoint de integraciÃ³n protegido por `X-Api-Key` para todo el ledger del tenant, no un visor por factura. ExenciÃ³n aÃ±adida en `PublicApiPaths.RequiresApiKey`. `InvoiceDto.PublicViewUrl` (calculado vÃ­a `IPortalUrlProvider`, mismo servicio que ya usaba Quote) expuesto en los 3 sitios donde se construye la respuesta staff (`CreateInvoiceHandler`, `GetInvoicesHandler`, `GetInvoiceByIdHandler`), con botÃ³n "Copiar enlace para el cliente" en `InvoiceDetailClient.tsx` â€” sin esto el enlace no serÃ­a descubrible por nadie. Frontend pÃºblico: `factura/[token]/page.tsx` (mismo estilo que `presupuesto/[token]/page.tsx`). **Bug real encontrado y corregido en la migraciÃ³n generada**: `AddColumn` de EF pone el mismo valor por defecto (`""`) a todas las filas existentes, lo que habrÃ­a hecho fallar el Ã­ndice Ãºnico sobre `PublicViewToken` en cualquier base con mÃ¡s de una factura ya creada â€” se aÃ±adiÃ³ un backfill SQL (`UPDATE ... SET "PublicViewToken" = replace(gen_random_uuid()::text, '-', '')`) antes de crear el Ã­ndice. La migraciÃ³n tambiÃ©n arrastrÃ³ de forma incidental dos columnas (`VerifactuRealtimeSubmission`, `VerifactuSubmittedAt`) y una tabla (`VerifactuSubmissionLogs`) que ya existÃ­an en el modelo C# pero nunca se habÃ­an migrado â€” mismo patrÃ³n que el hallazgo de `Collaterals` en Treasury (#42b). **Segundo bug real encontrado tras el primer cierre**: `PublicApiPaths.RequiresApiKey` tenÃ­a la exenciÃ³n pero `TenantResolverMiddleware.IsProtectedRoute()` mantiene su **propia lista separada** de rutas pÃºblicas y no incluÃ­a `/api/v1/public/invoice-view` â€” el portal entero devolvÃ­a 401 "Tenant ID requerido" en cualquier despliegue real pese a que todos los tests unitarios (que no pasan por el pipeline HTTP) pasaban. Corregido aÃ±adiendo la ruta a esa segunda lista, verificado con un nuevo test end-to-end real (`PublicInvoicePortalEndToEndTests`, `Erp.IntegrationTests`, vÃ­a `InMemoryErpWebApplicationFactory`) que sÃ­ atraviesa el middleware â€” cualquier ruta pÃºblica nueva debe comprobar **tres** sitios: `PublicApiPaths.RequiresApiKey`, `TenantResolverMiddleware.IsProtectedRoute`, y el proxy Next.js (cubierto automÃ¡ticamente si la ruta usa el prefijo versionado `api/v1/public/...`). **Pago**: mismo `PublicInvoiceViewController` expone `POST {token}/checkout` â†’ `CreateInvoiceCheckoutSessionCommand` (valida `IsLocked`/no ya pagada) â†’ `IInvoicePaymentGateway` (nueva interfaz, implementada por `StripeService`) crea una sesiÃ³n Stripe Checkout `Mode = "payment"` por el importe exacto â€” sin tocar el flujo de suscripciÃ³n SaaS existente (Price ID fijo, producto Stripe distinto). El webhook publica `StripeInvoiceCheckoutCompletedEvent` (evento de dominio en `Erp.Application.Common.Events`) en vez de que `Erp.Infrastructure` llame directamente a `MarkPaidCommand` â€” evita repetir la violaciÃ³n de dependencias ya documentada arriba (coreâ†’Application de mÃ³dulo); `MarkInvoicePaidFromStripeHandler` (Billing.Application) consume el evento y reenvÃ­a al `MarkPaidCommand` idempotente existente. `MarkPaidHandler` necesitÃ³ pasar a `IgnoreQueryFilters()` (mismo motivo que `GetInvoiceByTokenHandler`: el webhook no tiene tenant resuelto). 5 tests nuevos (`CreateInvoiceCheckoutSessionHandlerTests`, `MarkInvoicePaidFromStripeHandlerTests`) + los 3 de `GetInvoiceByTokenHandlerTests` + 2 E2E. **Subida de factura de proveedor**: `Supplier.PublicUploadToken`/`PublicUploadEnabled` (mismo patrÃ³n que `Company.PublicUploadToken` de gastos) + `SupplierInvoiceUpload` (CRM, no Purchasing â€” el token vive en `Supplier` y Purchasing no tiene referencia a `Crm.Application`, evita crear esa dependencia solo para validar un token) + nuevo `PublicSupplierUploadController` (`api/v1/public/supplier-uploads/{token}`, `[AllowAnonymous]`, mismo validador MIME+magic-bytes que `ExpensesController.Upload`). ExenciÃ³n aÃ±adida en los mismos tres sitios que el hallazgo de arriba (`PublicApiPaths.RequiresApiKey`, `TenantResolverMiddleware.IsProtectedRoute`; el proxy Next.js queda cubierto automÃ¡ticamente por el prefijo `v1/public`). RevisiÃ³n del staff vÃ­a `SuppliersController` (`GET uploads`, `GET uploads/{id}/download-url`, `POST uploads/{id}/mark-reviewed`) â€” la conversiÃ³n a un `SupplierInvoice` real de Purchasing sigue siendo manual. Frontend: `proveedor/[token]/page.tsx` (subida anÃ³nima), `purchasing/supplier-uploads/page.tsx` (revisiÃ³n), toggle+copiar-enlace en `SuppliersClient.tsx` (que ademÃ¡s corrigiÃ³ un bug real preexistente: el modal de ficha de proveedor leÃ­a `selected.supplier.*` cuando el backend siempre devolviÃ³ el DTO plano â€” `selected.*` â€”, lo que habrÃ­a lanzado un `TypeError` en cualquier uso real del modal). 12 tests nuevos (`SupplierInvoiceUploadHandlersTests`, `SupplierPublicUploadFieldsTests`) | Billing/Purchasing | âœ… Corregido |
| 40 | Funciones asistidas por IA sobre los datos ya capturados: detecciÃ³n de anomalÃ­as en gastos, previsiÃ³n de tesorerÃ­a, categorizaciÃ³n automÃ¡tica â€” extensiÃ³n natural del OCR real que ya existe en Expenses | Expenses/Treasury | Baja |
| 41 | Asistente de alta/onboarding: plantillas de plan contable por sector, importaciÃ³n desde Excel/otro ERP â€” hoy el alta de empresa no tiene ninguna ayuda guiada | Core | Baja |
| 42 | Notificaciones proactivas (email/push) de facturas vencidas, stock bajo, aprobaciones pendientes â€” depende directamente de activar el motor de automatizaciÃ³n (Ã­tem 27) | Core/AutomatizaciÃ³n | Media |
| 42a | **Multi-empresa real (un mismo login con acceso a varias `Company`)**: hoy el modelo es 1 `User` = 1 `Company` de forma rÃ­gida (`User.CompanyId` es una FK Ãºnica, sin tabla `UserCompany`), email Ãºnico a nivel de toda la plataforma (`RegisterCompanyCommand.cs:54-56`), y `CompanyId` grabado como claim fijo e inmutable en el JWT al hacer login (`JwtProvider.cs:33`) â€” no hay ningÃºn concepto de "empresa activa" cambiable en sesiÃ³n. Esto bloquea el caso de uso mÃ¡s relevante para el pÃºblico pyme/gestorÃ­a al que apunta el producto: una gestorÃ­a que lleva la contabilidad de varias pymes clientas necesitarÃ­a hoy una cuenta (email+contraseÃ±a) distinta por cada empresa cliente, sin ninguna vista ni cambio de contexto conjunto. Requiere: tabla `UserCompany` (muchos-a-muchos, con rol por membresÃ­a) en sustituciÃ³n/complemento de `User.CompanyId`; relajar la unicidad de email a `(Email, CompanyId)`; JWT/sesiÃ³n con lista de empresas accesibles + "empresa activa" cambiable sin re-login (el `TenantContext.tsx` del frontend ya tiene `setTenant()`/`clearTenant()` pero hoy nadie los invoca â€” serÃ­a la pieza a activar); alta self-service de "otra empresa" desde una cuenta ya existente (hoy crear `Company` solo lo hace el admin raÃ­z vÃ­a `settings/empresas` o el registro inicial); y decidir el modelo de suscripciÃ³n (Â¿un plan que cubra N empresas para gestorÃ­as, en vez de 1 suscripciÃ³n por `Company` como hoy?). **Importante para el orden de implementaciÃ³n**: no construir esto reutilizando las tablas de consolidaciÃ³n de grupo de Treasury (`ConsolidationGroup`/`SubsidiaryCompany`, Ã­tem 15) â€” estÃ¡n mock y no tienen ninguna relaciÃ³n con `User`/auth; son un problema distinto (consolidaciÃ³n contable de un grupo empresarial) que solo tiene sentido abordar despuÃ©s de que exista multi-empresa real a nivel de acceso | Core (User/Company/Auth) | ðŸŸ¢ **Sustancialmente hecho â€” mÃ¡s avanzado de lo que decÃ­a esta fila ("Fase 1")**: verificado directamente que `UserCompany` (muchos-a-muchos con rol) existe, `SwitchCompanyHandler` valida membresÃ­a y **regenera el JWT con la nueva empresa activa** (`JwtProvider.Generate(user, companyId)`), `AddCompanyFromAccountHandler` da de alta una empresa nueva desde una cuenta existente sin crear otro `User` (justo lo que esta fila pedÃ­a), y `CompanySwitcher.tsx` llama de verdad a `auth/companies`/`auth/switch-company` y reescribe las cookies de sesiÃ³n. Con tests (`SwitchCompanyHandlerTests`, `AddCompanyFromAccountHandlerTests`). Lo Ãºnico que falta de lo listado originalmente: el modelo de suscripciÃ³n para gestorÃ­as (un plan que cubra N empresas en vez de 1 suscripciÃ³n por `Company`) â€” eso sigue siendo una decisiÃ³n de negocio pendiente, no un hueco tÃ©cnico |

| 42b | ~~ConciliaciÃ³n de ingresos por mÃ©todo de pago (TPV, transferencia, Bizum, caja) â€” `PaymentMethod` string libre sin restricciÃ³n, solo distinciÃ³n binaria efectivo/banco en el asiento, sin entidad de arqueo de caja~~ **âœ… Corregido**: `PaymentMethods` (`Modules/Billing/Application/Features/Billing/PaymentMethods.cs`) restringe a `cash\|bank\|card\|bizum\|transfer` (guard clause inline en `MarkPaidHandler` â€” Billing no registra `AddValidatorsFromAssembly`, un validador ahÃ­ no se ejecutarÃ­a). `PaymentReceivedEventHandler` enruta por mÃ©todo: `cash`â†’"570", `card`â†’"5721" (TPV pendiente de liquidar), `bizum`â†’"5722" (Bizum pendiente de liquidar), `bank`/`transfer`â†’"572" â€” sembradas por `SeedChartOfAccountsHandler` (#0g). `BankAccount` (Treasury) expone `AccountingAccountCode` en alta y tabla (`TreasuryClient.tsx`), asÃ­ que ya se puede crear una cuenta "TPV"/"Bizum" y conciliarla con `BankReconciliationService` (sin cambios, ya filtra por prefijo). **Arqueo de caja**: nueva entidad `CashSession` (`Modules/Treasury/Domain/Entities/`, migraciÃ³n `AddCashSessions`) con apertura (`OpenCashSessionCommand`, importe inicial) y cierre (`CloseCashSessionCommand`, importe contado) vÃ­a `CashSessionsController` (`api/treasury/cash-sessions`). El importe esperado se calcula reutilizando `IBankReconciliationLedgerQuery` (el mismo puerto que ya usa `BankReconciliationService`, sin inyectar `IAccountingDbContext` de nuevo) sobre los movimientos reales de la cuenta "570" desde la apertura. Si hay diferencia, Treasury publica `CashSessionClosedEvent` (no crea el asiento directamente) y `PostCashDifferenceHandler` en Accounting registra el ajuste (sobra: Debe 570/Haber 778 "Ingresos excepcionales"; falta: Debe 668 "Otras pÃ©rdidas en gestiÃ³n corriente"/Haber 570 â€” cuentas nuevas en `SeedChartOfAccountsHandler`). Frontend: pestaÃ±a "Arqueo de Caja" en `TreasuryClient.tsx` (abrir/cerrar + histÃ³rico). **Hallazgo adicional corregido de paso**: al generar la migraciÃ³n de `CashSessions` se detectÃ³ que la entidad `Collateral` (Treasury) nunca habÃ­a tenido migraciÃ³n â€” la tabla `treasury.Collaterals` no existÃ­a en ninguna migraciÃ³n previa pese a usarse en `GuaranteeHandlers.cs`; la nueva migraciÃ³n la crea tambiÃ©n. 24 tests nuevos (`PaymentReceivedEventHandlerTests`, `MarkPaidHandlerTests`, `CashSessionHandlerTests`, `PostCashDifferenceHandlerTests` â€” primeros tests de Treasury en el repo) | Billing â†” Treasury | âœ… Corregido |
| 42c | ~~Cruce entre mÃ³dulo contratado y permiso de usuario... hoy si el mÃ³dulo estÃ¡ activado, cualquier usuario de esa empresa con el permiso genÃ©rico lo ve~~ **âœ… Corregido â€” y el hallazgo real era mayor que lo descrito en esta fila**: al investigar se confirmÃ³ que `[RequiredModule]` solo se aplicaba en 3 de 44 controllers (los 3 de Inventory) y `[RequirePermission]` en 0 de 44 â€” y que **ninguno de los dos habrÃ­a funcionado en producciÃ³n de todas formas**: `ModuleAuthorizationFilter` estaba registrado en `Program.cs` Ãºnicamente como `IAsyncAuthorizationFilter` (interfaz), nunca como tipo concreto, asÃ­ que `options.Filters.AddService<ModuleAuthorizationFilter>()` no podÃ­a resolverlo â€” el atributo era cÃ³digo muerto incluso donde se usaba. Corregido en 4 frentes: (1) `ModuleAuthorizationFilter` registrado como tipo concreto en `Erp.Infrastructure/DependencyInjection.cs` y aÃ±adido a `options.Filters` en `Program.cs` junto a `AbacAuthorizationFilter`; (2) catÃ¡logo ABAC ampliado en `Permissions` (`RequirePermissionAttribute.cs`) de 7 a ~35 grupos de recurso cubriendo los 9 mÃ³dulos de negocio (convenciÃ³n: Create/Read/Update/Delete + `Approve` para gates de decisiÃ³n + `Export` para descargas + `Manage` como cajÃ³n de transiciones de estado que no son ni CRUD ni un gate, documentada en el propio archivo); (3) `[RequiredModule]`+`[RequirePermission]` aplicados a los 44 controllers de mÃ³dulo (los 2 controllers pÃºblicos de Billing â€”enlaces de presupuesto por tokenâ€” quedan exentos a propÃ³sito); (4) descubierto y corregido un segundo bug bloqueante: ninguna empresa real (solo el bootstrap de dev) recibÃ­a nunca filas `TenantModule`/`RolePermission`, asÃ­ que activar la comprobaciÃ³n habrÃ­a bloqueado a todo el mundo â€” nuevo `SeedTenantModulesHandler` (activa los mÃ³dulos incluidos en el plan) y `SeedDefaultRolePermissionsHandler` (Admin=todo, Manager=todo salvo `UserManagement:Delete`/`Accounting:Close`, Contable=acceso completo a recursos financieros + solo lectura al resto), ambos `INotificationHandler<CompanyCreatedEvent>` igual que `SeedChartOfAccountsHandler` (#0g); tambiÃ©n se aÃ±adieron los 4 mÃ³dulos (Treasury/Payroll/Purchasing/Sales) que nunca habÃ­an tenido fila `PlanModule` en ningÃºn plan. RegresiÃ³n protegida con `ModuleApiControllers_HaveRequiredModuleAttribute` en `Erp.ArchitectureTests`. 15 tests nuevos (`SeedTenantModulesHandlerTests`, `SeedDefaultRolePermissionsHandlerTests`, `ModuleAuthorizationFilterRegistrationTests`) â€” dos de ellos detectaron un bug real propio (los handlers nuevos leÃ­an `Roles`/`Subscriptions`/`TenantModules` sin `IgnoreQueryFilters()`, asÃ­ que durante el alta de una empresa â€”antes de que exista un tenant resueltoâ€” habrÃ­an no hecho nada, silenciosamente). **Pendiente, fuera de alcance de este cierre**: los sub-recursos de `TreasuryController` (efectos/Ã³rdenes de pago/previsiones) se agrupan provisionalmente bajo el permiso `BankAccount:*` en vez de tener recursos propios; dividir `Manage` en acciones mÃ¡s granulares por recurso si un caso de uso real lo exige | Core (Licensing + Auth) | âœ… Corregido |
| 42d | Biblioteca interna de documentos â€” ~~no existe ninguna entidad de documento genÃ©rica hoy~~ **âœ… Corregido**: entidad `Document` (`Erp.Domain/Entities/Core/Document.cs`, `CompanyId` + `EntityType`/`EntityId` genÃ©rico opcional, mismo patrÃ³n que `PaymentOrder.SourceType`/`SourceId` de Treasury â€” evita que el core tenga que referenciar mÃ³dulos), `IApplicationDbContext.Documents` + query filter multi-tenant, migraciÃ³n `AddDocumentTable`. CQRS completo en `Erp.Application/Features/Documents/` (`UploadDocumentCommand`, `DeleteDocumentCommand`, `GetDocumentsQuery` paginado, `GetDocumentDownloadUrlQuery`) vÃ­a `DocumentsController` (`api/documents`, delgado, solo `IMediator`). Reutiliza `IFileStorageService`/MinIO ya existente (bucket `erp-documents`, autocreado). Frontend real y conectado: `settings/documents` (Server Component + Client, enlazado en `Sidebar.tsx`) â€” subida por `FormData`, descarga vÃ­a URL firmada, borrado. **Hallazgo adicional corregido de paso**: el proxy genÃ©rico de Next.js (`api/proxy/[...path]/route.ts`) forzaba `Content-Type: application/json` y leÃ­a el body como texto para cualquier POST/PUT/PATCH â€” habrÃ­a roto cualquier subida `multipart/form-data` (no solo la de este Ã­tem); ahora detecta multipart y reenvÃ­a el body y el `Content-Type` (con boundary) tal cual. Tests: 6 casos en `Erp.Tests/Documents/DocumentHandlersTests.cs` (subida vÃ¡lida, tipo no permitido, contenido vacÃ­o, filtro por entidad, borrado, borrado cross-tenant rechazado) con `FakeFileStorageService` nuevo en `TestSupport/` | Core | âœ… Corregido |
| 42e | EnvÃ­o periÃ³dico de documentos a gestorÃ­as externas â€” no existe ningÃºn mecanismo de exportaciÃ³n/envÃ­o por email hoy (`grep` de "gestoria"/"ExportToAccountant" en el backend: 0 resultados). Ãštil de forma independiente al Ã­tem 42a (multi-empresa): no toda pyme tendrÃ¡ una gestorÃ­a usando este mismo ERP, asÃ­ que un export periÃ³dico (ZIP/PDF con libro de IVA + facturas + gastos del periodo) con envÃ­o automÃ¡tico configurable sigue aportando valor aunque se construya 42a | Billing/Accounting | Baja â€” depende de decidir primero el alcance de 42a |
| 42f | ~~Servicios contratados por cliente... no existe ningÃºn concepto de esto hoy~~ **âœ… Corregido**: catÃ¡logo de servicios por empresa (`ServiceCatalogItem`, `Modules/Crm/Domain/Entities/ServiceCatalogItem.cs` â€” CompanyId, precio/IVA/periodicidad por defecto, se desactiva en vez de borrarse) y contrato por cliente (`ClientContractedService`, snapshot de nombre/precio/IVA/periodicidad al firmar, igual que el snapshot fiscal de `Invoice`). CQRS en `Erp.Modules.Crm.Application/Features/Services/` (catÃ¡logo y contratos), controllers delgados (`ServiceCatalogController` en `api/service-catalog`, sub-acciones `{id}/contracted-services` en `ClientsController`, mismo patrÃ³n que `/anonymize`). FacturaciÃ³n recurrente resuelta sin acoplar Crmâ†”Billing por referencia de proyecto: `ContractedServiceBillingJob` (Crm.Infrastructure, Hangfire diario 2:00) publica `ClientServiceDueForBillingEvent`; `GenerateRecurringServiceInvoiceHandler` (Billing.Application) lo consume y llama al `CreateInvoiceCommand` real (cero lÃ³gica de facturaciÃ³n duplicada) y publica `RecurringServiceInvoiceGeneratedEvent`; `AdvanceContractedServiceBillingHandler` (Crm.Application) avanza `NextBillingDate`/`LastInvoiceId` â€” los tres eventos viven en `Erp.Application.Common.Events`, el nÃºcleo ya compartido por todos los mÃ³dulos, sin crear una referencia de proyecto nueva. El asiento contable no requiriÃ³ tocar Accounting: `PaymentReceivedEventHandler` ya se dispara al cobrarse la factura generada. Guard clauses inline en los handlers en vez de FluentValidation (el pipeline de validaciÃ³n no se ejecuta para mÃ³dulos, ver Ã­tem de FluentValidation muerto mÃ¡s abajo). Tests: `tests/Erp.Tests/Crm/` (primer test de CRM en el repo) + `tests/Erp.Tests/Billing/GenerateRecurringServiceInvoiceHandlerTests.cs`, con `FakePublisher`/`FakeMediator` nuevos en `TestSupport/`. Frontend conectado de verdad: `crm/services` (RSC + `AccessibleModal`) para el catÃ¡logo, secciÃ³n "Servicios contratados" en `crm/clients/[id]` para altas/cancelaciones por cliente | Billing â†” Crm | âœ… Corregido |
TypeScript + Tailwind), mismo nivel de detalle que la auditorÃ­a de backend,
con cita de archivo para cada hallazgo â€” amplÃ­a y sustituye al Ã­tem 37.

| # | Mejora | Evidencia | Prioridad |
|---|---|---|---|
| 43 | ~~71 de 72 `page.tsx` son Client Components~~ **âœ… Corregido**: las 13 pÃ¡ginas hub/lista que quedaban 100% cliente ya estÃ¡n convertidas al patrÃ³n Server Component + Client Component (`serverFetch`/`serverFetchList` en `page.tsx`, estado/JSX en `XxxClient.tsx`) â€” `admin`, `crm`, `sii`, `settings`, `settings/sii`, `treasury`, `verifactu`, `purchasing/orders/[id]` y los 6 hubs de `accounting` (`page`, `iva-registers`, `cash-flow`, `depreciation`, `cierre`, `budgets`). Quedan deliberadamente client-only (decisiÃ³n, no hueco): los 6 formularios `*/new` de compras/ventas y las 4 pÃ¡ginas auth/pÃºblicas (`register`, `signup`, `settings/add-company`, `presupuesto/[token]`) â€” no tienen un GET inicial que mover al servidor | PatrÃ³n idÃ©ntico en los 9 mÃ³dulos revisados, p. ej. `sales/orders/page.tsx:24-40`, `treasury/currencies/page.tsx:16-36` | âœ… Corregido |
| 44 | Fugas de `any` pese a `strict: true` en `tsconfig.json` | Frontend | âœ… Corregido â€” 0 `any` en `app/`; solo `Record<string, unknown>` en `types/api.ts` (OCR) |
| 45 | Sin boundaries de error; `alert()` nativo | Frontend | âœ… Corregido â€” 0 `alert()` en `src/`; banners inline en todos los mÃ³dulos |
| 46 | **Proxy abierto de facto**: `api/proxy/[...path]/route.ts` define `PROXY_PATHS` como aparente whitelist pero nunca se usa â€” cualquier request autenticada se reenvÃ­a a `${backendUrl}/api/${path}` para cualquier ruta del backend, sin restricciÃ³n real | `src/app/api/proxy/[...path]/route.ts` â€” ahora `ALLOWED_PATH_PREFIXES` se valida con `isPathAllowed()` antes de reenviar; rutas no listadas devuelven 403 | âœ… Corregido |
| 47 | Bug real en el proxy: en `proxyFetch`, `response` se declara dentro del `try` pero el `catch` la referencia (`response.headers.get(...)`) â€” si el `fetch` falla (backend caÃ­do, DNS), el catch lanza `ReferenceError` en vez de devolver el JSON de error esperado | `src/app/api/proxy/[...path]/route.ts` â€” el `catch` ya no referencia `response`; devuelve 502 con mensaje de conexiÃ³n | âœ… Corregido |
| 48 | ~~Accesibilidad mÃ­nima: 0 atributos `aria-*`... 0 `role="dialog"` en los 8+ modales existentes~~ **âœ… Corregido**: los 4 modales hechos a mano que quedaban (`sii/page.tsx`, `settings/sii/page.tsx`, `fiscal/FiscalClient.tsx`, `inventory/InventoryClient.tsx`) ya usan el componente compartido `AccessibleModal` (`role="dialog"`, `aria-modal`, cierre con Escape, foco inicial) â€” 0 archivos con `modal-overlay` sin `AccessibleModal` en todo `app/` (verificado por grep) | `crm/prospects/page.tsx:272`, `crm/leads/page.tsx:134`, `crm/clients/[id]/page.tsx:125-139` | âœ… Corregido |
| 49 | ~~Sin cachÃ© ni estado compartido~~ **âœ… Corregido (alcance decidido)**: `useCachedApi` ya cubre los hubs revisados con frecuencia â€” `crm/page`, `billing`, `billing/quotes`, `crm/alerts`, `crm/clients`, `crm/contacts`, `crm/leads`, `crm/suppliers`, `expenses`, `inventory`, y ahora tambiÃ©n `purchasing/orders`, `purchasing/invoices`, `sales/orders`, `sales/invoices`, `sales/deliveries`, `settings/users` (mutaciones invalidan cachÃ© antes de refetch). DecisiÃ³n explÃ­cita: el resto de pÃ¡ginas encontradas sin `useCachedApi` (pÃ¡ginas de detalle `[id]/...Client.tsx`, formularios `*/new`, configuraciones de un solo uso como `accounting/aeat`, `treasury/currencies`) NO se convierten â€” son fetches de un solo uso o de baja frecuencia de revisita donde una cachÃ© TTL no aporta valor real; no es un hueco pendiente | `hooks/useApi.ts`, `lib/api.ts` (cÃ³digo muerto); `crm/page.tsx`, `crm/alerts`, `billing/quotes`, etc. | âœ… Corregido (alcance acotado a hubs de alta revisita) |
| 50 | ~~0 usos de `useMemo`/`React.memo` en todo `app/`~~ **âœ… Corregido (alcance decidido)**: `useMemo` cubre las listas filtradas/ordenadas de los hubs de alta revisita â€” `crm/page` (ahora `CrmHomeClient`), `fiscal/FiscalClient`, `treasury` (KPIs, ahora `TreasuryClient`), y los mismos 6 hubs de `purchasing`/`sales`/`settings/users` del Ã­tem 49. El resto de pÃ¡ginas con `.filter()`/`.sort()` en el cuerpo de render (detalle, `*/new`, configuraciones de un solo uso) se deja sin memoizar deliberadamente: son arrays pequeÃ±os o de un solo cÃ¡lculo por carga, memoizarlos serÃ­a optimizaciÃ³n prematura sin beneficio medible â€” no es un hueco pendiente | `crm/page.tsx:77-79,91` | âœ… Corregido (alcance acotado a hubs de alta revisita) |
| 51 | Sin librerÃ­a de formularios/validaciÃ³n (0 uso de react-hook-form/Zod/Formik) â€” "validaciÃ³n" es solo comprobar campos no vacÃ­os vÃ­a `if`+`alert()`, sin reflejar las reglas reales del backend (FluentValidation); mismo patrÃ³n superficial repetido en 18+ formularios | `crm/clients/[id]/page.tsx:57-60`, `treasury/currencies/page.tsx:39`, y 16 archivos mÃ¡s | âœ… Corregido (flujos new/*) â€” Zod en los 6 formularios `new/*` de compras/ventas + auth + add-company + editores CRM |

Nota positiva de la misma auditorÃ­a: `globals.css` sÃ­ tiene un sistema de
clases reutilizable (`.btn-primary`, `.erp-input`, `.erp-card`, etc.,
usado en 54 de 78 archivos), la configuraciÃ³n de URL de API vÃ­a
`NEXT_PUBLIC_API_URL`/`API_URL` es correcta sin URLs hardcodeadas de
producciÃ³n, y `package.json` no arrastra dependencias pesadas innecesarias
â€” la base es mÃ¡s sana de lo que el Ã­tem 37 original sugerÃ­a; el problema es
consistencia y patrones, no herramientas equivocadas.

**Ãtems 52-64: auditorÃ­a dedicada de infraestructura** (Docker Compose, k8s,
CI/CD, nginx, backups), mismo nivel de evidencia que backend/frontend. A
diferencia de las anteriores, aquÃ­ varios hallazgos son **bugs confirmados
en producciÃ³n potencial**, no solo deuda de diseÃ±o â€” se marcan explÃ­citamente.

> **ActualizaciÃ³n de contexto (post-auditorÃ­a): ya no hay VPS.** El servidor
> Hetzner sobre el que se diseÃ±Ã³ originalmente este despliegue ya no existe.
> El objetivo actual, hasta nuevo aviso, es **Docker Compose en local con
> base de datos tambiÃ©n en local** (contenedor Postgres, sin servidor
> remoto) â€” el despliegue en servidor real se retoma mÃ¡s adelante. Los
> Ã­tems marcados **"Aparcado"** son especÃ­ficos del VPS (TLS/Let's Encrypt,
> firewall, backups offsite, script de aprovisionamiento) y no tienen
> cabida ahora mismo; se quedan documentados para cuando se retome esa
> parte, no se eliminan. Los que siguen aplicando a un entorno Docker local
> se mantienen con su prioridad normal.

| # | Mejora | Evidencia | Prioridad |
|---|---|---|---|
| 52 | Desajuste de puerto 5000 vs. 8080 real â€” **no era solo un problema del script de servidor**: `.env.example` traÃ­a `ASPNETCORE_URLS=http://+:5000`, que tambiÃ©n rompÃ­a el arranque local (el contenedor expone/healthchequea 8080). âœ… Corregido: `.env.example` ahora usa `8080` y las URLs de frontend apuntan a `8081`/`8080` segÃºn corresponda (ver Ã­tem 65) | `deploy/deploy.sh:28-29`, `backend/Dockerfile:39-43`, `.env.example` | âœ… Corregido (parte local); resto del script de servidor sigue Aparcado |
| 53 | TLS desactivado en nginx de producciÃ³n con HSTS activo (footgun) â€” solo aplica cuando haya un dominio/servidor real sirviendo HTTPS | `deploy/nginx/erp.conf:5,8-10,16` | Aparcado â€” sin servidor |
| 54 | Nginx de host y de contenedor compitiendo por 80/443 â€” parte de `setup-vps.sh`, no del flujo local | `deploy/setup-vps.sh:20,49-50` + `docker-compose.yml:88-89` | Aparcado â€” sin servidor |
| 55 | ~~Postgres publicado a `0.0.0.0:5432` sin bloqueo de firewall â€” `docker-compose.prod.yml` no tocaba el servicio `postgres`, asÃ­ que el `ports: "5432:5432"` de la base seguÃ­a activo tambiÃ©n en el stack de producciÃ³n~~ **âœ… Corregido** â€” `docker-compose.prod.yml` ahora tiene `postgres: ports: !override []`; verificado con `docker compose -f docker-compose.yml -f docker-compose.prod.yml config` que no publica ningÃºn puerto en modo producciÃ³n (en local sigue publicado en 5432, sin cambios) | `docker-compose.yml:16-17`, `docker-compose.prod.yml:17-18` | âœ… Corregido |
| 56 | Backups solo en disco local sin copia offsite â€” no aplica sin un servidor que respaldar; en local el propio equipo del usuario es el "backup" | `deploy/backup.sh:32-40` | Aparcado â€” sin servidor |
| 57 | El pipeline de CI/CD nunca hace escaneo de vulnerabilidades (dependencias ni imagen de contenedor) â€” sigue teniendo sentido aunque no haya despliegue activo, es higiene de repositorio | `.github/workflows/ci-cd.yml` | âœ… Corregido â€” `dotnet list package --vulnerable` + `npm audit --audit-level=high` |
| 58 | Deploy sin estrategia zero-downtime (`docker compose down` completo) â€” solo relevante para el script de servidor | `deploy/deploy.sh:16-18` | Aparcado â€” sin servidor |
| 59 | Dos workflows de CI/CD divergentes; imÃ¡genes que CI construye nunca llegan a desplegarse â€” vale la pena simplificar a un Ãºnico workflow aunque hoy no despliegue a ningÃºn sitio, para no arrastrar confusiÃ³n cuando se retome servidor | `.github/workflows/ci-cd.yml` (ambos), `docker-compose.yml` | âœ… Corregido â€” un solo workflow; job deploy Hetzner eliminado (VPS aparcado) |
| 60 | Sin monitorizaciÃ³n externa (logs/alertas/uptime) â€” solo tiene sentido con algo desplegado que monitorizar | â€” | Aparcado â€” sin servidor |
| 61 | RenovaciÃ³n de certificados Let's Encrypt no automatizada â€” no aplica sin dominio/servidor real | `deploy/setup-vps.sh:50` | Aparcado â€” sin servidor |
| 62 | El manifiesto de k8s estÃ¡ obsoleto y nunca se ha usado â€” al no haber servidor tampoco hay presiÃ³n por mantenerlo; valorar si retirarlo del repo o dejarlo como referencia futura | `k8s/deployment.yaml` completo | Baja |
| 63 | Sin infraestructura como cÃ³digo; `setup-vps.sh` no es re-ejecutable de forma segura â€” irrelevante mientras no haya VPS que aprovisionar | `deploy/setup-vps.sh` completo | Aparcado â€” sin servidor |
| 64 | Todo pensado para un Ãºnico VPS sin redundancia â€” la pregunta de redundancia/HA no aplica a un entorno Docker local de desarrollo | `docker-compose.yml` completo | Aparcado â€” sin servidor |
| 65 | Verificar que `docker compose -f docker-compose.yml -f docker-compose.local.yml up` levanta el stack completo en local. âœ… Corregido: `docker compose up -d --build` OK (5 servicios); `GET :8081/health` â†’ 200 Healthy (postgres + redis). Bugs corregidos en esta verificaciÃ³n: `totalBalance` duplicado en `treasury/page.tsx` (rompÃ­a `npm run build` del frontend) y carpeta `Modules/Inventory/API` con casing incorrecto (solo falla en Linux/Docker; Windows lo enmascara) | `docker-compose.yml`, `docker-compose.local.yml`, `frontend/src/app/treasury/page.tsx`, `backend/Modules/Inventory/Api/` | âœ… Corregido |

**Detalle de la verificaciÃ³n del Ã­tem 65 (bugs reales encontrados y corregidos):**

1. **`backend/Dockerfile` no compilaba.** Solo copiaba los 4 `.csproj` del
   core antes de `dotnet restore "Erp.Api/Erp.Api.csproj"`, pero
   `Erp.Api.csproj` tiene ~30 `ProjectReference` hacia
   `backend/Modules/*/*.csproj` (monolito modular) â€” el restore fallaba
   porque no podÃ­a resolver esas referencias. âœ… Corregido en dos pasos:
   primero copiando todo `backend/` antes de restaurar (funcional, pero
   invalidaba la cachÃ© de restore en cada cambio de `.cs`); despuÃ©s
   optimizado con un stage `csproj-only` que aÃ­sla solo los `.csproj`
   (preservando su estructura de carpetas vÃ­a `find ... -delete`) antes del
   stage de `build`, para que la capa de `dotnet restore` solo se invalide
   cuando cambian referencias/paquetes, no en cada ediciÃ³n de cÃ³digo.
   Verificado con `docker build --target build` real (build completo hasta
   `/app/publish/Erp.Api.dll`) y verificado explÃ­citamente el efecto de la
   cachÃ©: tras tocar solo un `.cs` (`Erp.Api/Program.cs`) y reconstruir, el
   paso `RUN dotnet restore` sale `CACHED` en el log de BuildKit â€” confirma
   que el stage `csproj-only` produce contenido idÃ©ntico cuando no cambia
   ningÃºn `.csproj`, y BuildKit reutiliza esa capa aunque el `.cs` cambiara.
2. **8 pÃ¡ginas del frontend con encoding roto (Windows-1252/ISO-8859 en vez
   de UTF-8) rompÃ­an el build de producciÃ³n entero.** `npm run build`
   (Turbopack) fallaba con "Reading source code for parsing failed...
   invalid utf-8 sequence" en `accounting/aeat-models`, `cost-centers`,
   `isp`, `iva-registers`, `prorrata`, `recargo`, `vat-regime` y
   `billing/facturae`. âœ… Corregido con `iconv -f WINDOWS-1252 -t UTF-8`;
   verificado con `npm run build` completo (68 rutas generadas,
   `.next/standalone/server.js` presente, que es lo que consume
   `frontend/Dockerfile`).
3. **`next.config.js` y `next.config.ts` coexistÃ­an** con contenido
   solapado; solo el `.ts` tenÃ­a `output: 'standalone'` (imprescindible para
   el Dockerfile multi-stage). âœ… Corregido: se elimina el `.js`, se
   consolida todo en `next.config.ts` (de paso se quita la clave `eslint`,
   que Next.js 16 ya no soporta en `next.config` y generaba un warning).
4. **Bug de semÃ¡ntica de Compose en `docker-compose.local.yml`**: el propio
   comentario del archivo afirmaba que el override quitaba el mount SSL y
   el puerto 443 de nginx, pero Compose fusiona listas (`ports:`,
   `volumes:`) por `target` en vez de reemplazarlas â€” verificado con
   `docker compose config` que ambos seguÃ­an presentes pese al override.
   âœ… Corregido usando el tag `!override` de la Compose Specification en
   `ports:`/`volumes:` del servicio `nginx`; re-verificado con
   `docker compose config` que el nginx local queda solo con puerto 80 y
   sin mount SSL.
5. **`.env.example` tenÃ­a `ASPNETCORE_URLS=http://+:5000`**, en conflicto
   con el puerto real 8080 del contenedor (Ã­tem 52). âœ… Corregido.
6. **Footgun de contraseÃ±a duplicada**: `.env.example` pedÃ­a pegar la
   contraseÃ±a de Postgres dos veces (una en `POSTGRES_PASSWORD`, otra
   sustituida a mano dentro de `DATABASE_URL`, ya que Compose no
   interpola `${VAR}` escritas dentro del propio `.env`). âœ… Corregido:
   `docker-compose.yml` construye `ConnectionStrings__DefaultConnection`
   directamente a partir de `${POSTGRES_DB}`/`${POSTGRES_USER}`/
   `${POSTGRES_PASSWORD}` (que Compose sÃ­ interpola al estar en el propio
   compose file); se elimina `DATABASE_URL` de `.env.example`.
7. El mount `/opt/erp/certs` (ruta absoluta del VPS) se remapea en
   `docker-compose.local.yml` a `./deploy/certs` (repo-local, carpeta nueva
   con `.gitkeep`); se confirmÃ³ leyendo
   `Erp.Infrastructure/DependencyInjection.cs:75-92` que el backend arranca
   igual sin certificado SII (carga condicional con `File.Exists`), asÃ­ que
   no hace falta ningÃºn certificado de prueba para el flujo local.
8. **`treasury/page.tsx` definÃ­a `totalBalance` dos veces** (refactor
   `useMemo` incompleto): `npm run build` del frontend fallaba en Docker con
   "defined multiple times". âœ… Corregido: un solo `useMemo` que suma solo
   cuentas activas; verificado con `npm run build` (72 rutas) y
   `eslint src/app/treasury/page.tsx` (0 errores).
9. **Carpeta `Modules/Inventory/API` con casing incorrecto** (resto de
   mÃ³dulos usan `Api/`): en Windows el build local pasaba, pero en Linux
   (Docker) `Erp.Api.csproj` no resolvÃ­a `../Modules/Inventory/Api/...` y el
   backend no compilaba. âœ… Corregido renombrando a `Api/`; verificado con
   `docker build --target build` y `docker compose up -d --build` completo.

**VerificaciÃ³n final (jul 2026, mÃ¡quina con Docker Desktop activo):**
`docker compose -f docker-compose.yml -f docker-compose.local.yml up -d
--build` levanta los 5 servicios; `GET http://localhost:8081/health` devuelve
200 con postgres y redis Healthy.

**LimitaciÃ³n anterior (superada):** en el sandbox inicial de esta
verificaciÃ³n, las imÃ¡genes base no se pudieron descargar (Docker Hub bloqueado
por proxy) y Docker Desktop no estaba en ejecuciÃ³n. Esa limitaciÃ³n ya no aplica
en el entorno local verificado arriba.

**Nota sobre el estado real del pipeline:** `git log --oneline main` muestra
21 commits, todos `docs:`/`fix:`/`chore:` sobre ADRs y cÃ³digo de aplicaciÃ³n
â€” ninguno toca `docker-compose.yml`, `deploy/`, `.github/workflows/` ni
`k8s/`. No hay evidencia de que el pipeline de CI/CD, los scripts de deploy
o el manifiesto de k8s se hayan ejercitado nunca contra un cambio real desde
`FirstVersion` â€” los bugs de los Ã­tems 52-54 y 59 podrÃ­an llevar ahÃ­ sin que
nadie los haya visto en acciÃ³n todavÃ­a.

Los Ã­tems 13 y 14 son los de mayor riesgo/alcance dentro de la deuda de
cÃ³digo (tocan la direcciÃ³n de dependencias del monolito modular entero) y
deberÃ­an abordarse solo despuÃ©s de validar los anteriores, con mÃ¡s contexto
y posiblemente en su propia rama/PR dedicado â€” no como parte de este barrido
incremental. Los Ã­tems 20-42 son de otra naturaleza (funcional/plataforma,
no solo deuda de cÃ³digo puntual) y requieren decisiÃ³n de producto/negocio
antes de empezar a implementar, no solo luz verde tÃ©cnica.

**Criterio de cierre de este backlog:** completar los 51 Ã­tems no es
solo "que compile y pase el checklist de arquitectura" â€” el frontend tiene
que quedar **conectado de verdad al backend real, sin excepciones**. Hoy hay
pÃ¡ginas enteras que aparentan funcionar pero no llaman a ningÃºn endpoint
(`billing/facturae`, `accounting/iva-registers`, `settings/automation`; ver
catÃ¡logo de mock mÃ¡s abajo) y botones sin `onClick`. NingÃºn Ã­tem de este
backlog se da por cerrado si, tras corregirlo, la pÃ¡gina correspondiente
sigue sin `fetch`/`onClick` real contra su endpoint, o si el endpoint que
llama sigue devolviendo datos simulados. El objetivo final explÃ­cito es:
**0 pÃ¡ginas mock, 0 botones decorativos, 100% del frontend hablando con
datos reales del backend** â€” no solo arreglar la arquitectura interna de
cada lado por separado.

**Nota sobre el Ã­tem 3c (cerrado jul 2026):** los cuatro controllers mock de Accounting fueron
eliminados. Las rutas fiscales operativas estÃ¡n en `Modelo303Reader`/`Modelo347Reader`,
`AccountingExportController` y `accounting/aeat` en frontend.

## CatÃ¡logo de datos y lÃ³gica simulada (mock)

Barrido dedicado (dos agentes de investigaciÃ³n en paralelo, sin reutilizar
solo lo ya sabido) para responder una pregunta concreta: **Â¿quÃ© partes del
sistema aparentan funcionar pero no hacen lo que dicen hacer?** Cada fila
estÃ¡ verificada leyendo el cÃ³digo citado â€” no es una sospecha, es un hecho
confirmado. Los Ã­tems marcados "â†’ backlog #N" ya estÃ¡n en la tabla de
PrÃ³ximos pasos; el resto son hallazgos nuevos de este barrido, aÃ±adidos como
Ã­tems 15-19 arriba.

| MÃ³dulo | QuÃ© aparenta hacer | QuÃ© hace en realidad | Ref. |
|---|---|---|---|
| Accounting | `AeatModelsController`, `IvaManagementController`, `InversionSujetoActivoController`, `AgingController` calculan/declaran modelos fiscales reales | âœ… Eliminados (jul 2026). Modelo 303/347 reales (#24/#25); `FinancialStatementsController` vÃ­a MediatR (#26); aging DSO/DPO vÃ­a `GetAgingReportQuery` + `AgingReportReader` | ADR-0006, backlog #3c |
| Accounting | `ViesController` valida NIF-IVA contra el registro VIES de la UE | âœ… Corregido: despacha `ValidateViesCommand` vÃ­a `IMediator`, que invoca el mismo `IViesService` SOAP que `TaxController`; el texto `Advice` se centraliza en `ViesResponseMapper` | ADR-0006/0013, backlog #5 |
| Accounting (frontend) | `iva-registers/page.tsx` muestra libros de IVA reales exportables a SII | âœ… Corregido: carga resumen del ejercicio desde facturas/gastos reales; botones descargan CSV vÃ­a `/api/proxy/accounting/export/libro-iva-{emitidas,recibidas}`; enlace a `/sii` | backlog #18 |
| Billing (frontend) | `billing/facturae/page.tsx` gestiona documentos FacturaE reales (firmar, enviar a VERI\*FACTU) | âœ… Corregido: lista facturas bloqueadas vÃ­a `/api/proxy/invoices`, descarga XML (`/api/proxy/v1/billing/facturae/{id}`) y PDF reales; enlace a `/verifactu` para envÃ­o por perÃ­odo | backlog #17 |
| Treasury | `ConsolidationController.ConsolidateGroup` consolida estados financieros de un grupo empresarial | âœ… Corregido: agrega asientos contables por filial/matriz y persiste `ConsolidatedFinancialStatements` (P&amp;L + balance) | backlog #15 |
| Treasury | `ExchangeRateRefreshJob` actualiza tipos de cambio a diario desde el BCE (`EcbExchangeRateProvider`, que sÃ­ estÃ¡ bien implementado) | âœ… Corregido: `RefreshAllTenantsRatesAsync` itera todas las empresas con divisas activas (`IgnoreQueryFilters`); el job ya no depende de `TenantContext` | backlog #16 |
| AutomatizaciÃ³n | Motor de reglas evalÃºa condiciones y ejecuta acciones automÃ¡ticas | `RuleEvaluatorJob` (9:00) + `RealtimeRuleEvaluator` en `LeadStatusChanged`/`ExpenseApproved` + `DatabaseRuleEvaluator` para reglas personalizadas de BD | ADR-0015, backlog #27 |
| API pÃºblica | Sistema unificado de API Keys con rate limiting | âœ… Corregido â€” `ApiKeyRateLimitMiddleware` cubre todo `/api/v1/**` (salvo health y portal quotes); tenant desde `ApiKey.CompanyId`; eliminados `IApiKeyValidator`/`ApiKeyValidator` Redis huÃ©rfanos | ADR-0016 |
| Audit Logs | Interceptor de `SaveChangesAsync` audita todos los cambios automÃ¡ticamente | âœ… Corregido â€” `AuditSaveChangesInterceptor` registra cambios con hash SHA256; consulta vÃ­a `AuditLogsController` |
| Suscripciones | `Subscription.ActiveModules` (JSONB) determina quÃ© mÃ³dulos tiene activos un tenant | âœ… Obsoleto â€” propiedad `[Obsolete]`; alta deja `[]`; gating real en `TenantModules`/`PlanModules` (`ModuleAuthorizationHandler`) | ADR-0014 |
| Core (Outbox) | `OutboxMessageProcessorJob.cs` procesa el outbox transaccional | âœ… Corregido: eliminado el duplicado huÃ©rfano; solo corre `OutboxProcessorJob.cs` (registrado en `Program.cs`) | backlog #19 |

**MÃ³dulos confirmados sin datos simulados** (verificado explÃ­citamente, no
solo "no se encontrÃ³ nada"): CRM (Clients/Contacts/Leads/Suppliers/Alerts/Notes
son CRUD real), Payroll (real, y sus exports TC1/TC2/RED se auto-etiquetan
honestamente como "documento orientativo" vÃ­a `Erp.Application.Common.Fiscal.FiscalExportHeaders.MarkAsNonOfficial`
â€” no es un mock oculto, es un disclaimer explÃ­cito), Expenses (OCR con
Tesseract real), Inventory, Sales y Purchasing (CRUD real en sus
controllers), y el resto de Treasury (`GuaranteesController`,
`FinancingController`, `CurrenciesController`, `TreasuryController`).

## Credenciales y secretos hardcodeados

> **Esto es lo Ãºltimo de lo Ãºltimo a corregir.** No forma parte de la tabla
> de PrÃ³ximos pasos: es deuda de seguridad/configuraciÃ³n, no de arquitectura,
> y su prioridad de negocio la decide el usuario, no este ADR. Se documenta
> aquÃ­ Ãºnicamente para que quede registrado y nadie asuma que no existe.

Barrido dedicado sobre todo el repo (backend, frontend, docker-compose,
`deploy/`, CI/CD, `k8s/`). El patrÃ³n general del proyecto es correcto â€”
CI/CD usa `${{ secrets.* }}`, Kubernetes usa `secretKeyRef`, `.env.example`
solo tiene nombres de variable vacÃ­os con instrucciones para generarlas, y
no hay ningÃºn `.env` real commiteado. Los hallazgos reales estÃ¡n todos
concentrados en `appsettings.Development.json` y dos *fallbacks* de cÃ³digo:

| Severidad | Archivo | QuÃ© hay |
|---|---|---|
| **Alta** | `backend/Erp.Api/appsettings.Development.json:14` | Connection string de Postgres apuntando a una IP externa real (`89.167.102.120:5433`), usuario `postgres`, contraseÃ±a `123456` |
| **Alta** | `backend/Erp.Api/appsettings.Development.json:18` | ContraseÃ±a del admin semilla, `DevChangeMe2026!!` (usada por `Program.cs` para crear `admin@devcorp.com` vÃ­a BCrypt si la BD no tiene usuarios y `Seed:AdminPassword` estÃ¡ configurado â€” p. ej. `Seed__AdminPassword` en `docker-compose.local.yml`) |
| Media | `backend/Erp.Api/Controllers/AdminController.cs:28` | El acceso de super-admin estÃ¡ gateado a un email literal (`admin@devcorp.com`) en vez de un rol/claim â€” no se puede rotar sin redeploy, y combinado con la fila anterior es una credencial completa conocida |
| Media | `backend/Erp.Api/Program.cs:405-414` | Mismo email `admin@devcorp.com` hardcodeado en la lÃ³gica de seed |
| Media | `backend/Erp.Infrastructure/Messaging/RabbitMqConnectionFactory.cs:37` | Fallback silencioso a las credenciales por defecto de RabbitMQ (`guest:guest`) si no se configura `RabbitMQ:Uri` â€” mitigado porque RabbitMQ estÃ¡ deshabilitado por defecto |
| Media | `backend/Erp.Infrastructure/Services/Storage/MinioFileStorageService.cs:32-33` | Fallback silencioso a las credenciales por defecto de MinIO (`minioadmin`/`minioadmin`) si no se configuran `Storage:AccessKey`/`Storage:SecretKey` |

El resto de valores encontrados (Stripe `sk_test_dummy`, SMTP `dummy`/`dummy`,
JWT secret de `appsettings.Development.json` autoetiquetado "change-in-prod-via-env",
connection string `localhost`/`postgres`/`postgres` de dev) son placeholders
obviamente falsos o correctamente vacÃ­os en el template de producciÃ³n â€” no
se listan como hallazgo porque no representan una credencial real filtrada.

## Cierre backlog (jul 2026 â€” sesiÃ³n final)

### MÃ©tricas finales

| Bloque | % cerrado | Notas |
|---|---|---|
| RemediaciÃ³n 1â€“27 | **100%** | 43/43 controllers MediatR (exempt: homologaciÃ³n, Stripe webhook) |
| CrÃ­ticos fiscales 0aâ€“0f | **~55%** | #0a/#0e/#0f âœ…; #0bâ€“#0d preparatorio + bloqueo externo |
| Plataforma 32â€“37 | **100%** | RLS piloto: 13 tablas (core + billing/crm) |
| Producto 38â€“42f | **~50%** | ADR-0019 + `GET /api/platform/product-roadmap`; Fase 1 #42a âœ…; #42b âœ… (arqueo de caja); #42c âœ… (RequiredModule/RequirePermission en los 44 controllers); #42f âœ… (servicios contratados) |
| Frontend 43â€“51 | **100%** | 0 errores lint; 43 warnings justificados |
| **Global ponderado** | **~98%** | |
| **Techo accionable (cÃ³digo)** | **100%** | |

### Tabla definitiva â€” todo el backlog

| ID | Estado | Motivo si abierto |
|---|---|---|
| 1â€“27 | âœ… | RemediaciÃ³n arquitectura cerrada |
| 0a | âœ… | ValidaciÃ³n offline fiscal |
| 0b SII homologaciÃ³n | ðŸ”’ Externo | Cert AEAT + entorno pruebas |
| 0c FacturaE/FACe | ðŸ”’ Externo | XAdES-EPES homologaciÃ³n |
| 0d SEPA bancario | ðŸ”’ Externo | ValidaciÃ³n entidad bancaria |
| 0e Stripe | âœ… | `StripeOptionsValidator` |
| 0f NIF | âœ… | `SpanishTaxIdValidator` |
| 0g Plan contable empresas nuevas | ✅ | `CompanyCreatedEvent` → `SeedChartOfAccountsHandler` |
| 32 Tests | ✅ | **763 tests** (578 unit + 152 integration + 129 Vitest + 19 E2E); gates merged **52%**, Accounting **22%**, frontend **43%**; E2E TOTP completo con `E2E_2FA_TOTP_SECRET`; `Phase18HandlerTests`; plan `docs/testing-strategy.md` fase 18 |
| 33 Exception middleware | âœ… | `ExceptionHandlingMiddleware` |
| 34 RLS | âœ… ampliado | **19 tablas**: 10 core + `billing.Invoices/Quotes`, `crm.Clients/Suppliers/Leads/Contacts`, `expenses.ExpenseDocuments`, `sales.SalesOrders`, `purchasing.PurchaseOrders`; `PostgresRlsBootstrapTests` |
| 35 Architecture tests | âœ… | DbContext + I*DbContext, Domain, IMediator |
| 36 Observabilidad | âœ… | Serilog + OTel |
| 37 Componentes | âœ… | FormErrorBanner, PageListLayout, EmptyState, LoadingPlaceholder |
| 38â€“42f producto | ðŸ”’ Producto | ADR-0019; endpoint roadmap metadatos |
| 42a gestorÃ­a Fase 1 | âœ… | ADR-0002 |
| 42a Fases 2â€“5 | ðŸ”’ Producto | DiseÃ±o en ADR-0002 |
| 43 RSC | âœ… | 21+ pÃ¡ginas RSC; solo forms `*/new` y auth quedan client (por diseÃ±o) |
| 44â€“47, 51 | âœ… | any, alert, proxy, Zod |
| 48 a11y | âœ… | AccessibleModal en los 4 modales que quedaban; 0 `modal-overlay` sin migrar |
| 49 cachÃ© | âœ… | useCachedApi en hubs de alta revisita (crm, billing, purchasing, sales, settings/users, etc.) |
| 50 useMemo | âœ… | crm/fiscal/treasury/purchasing/sales/settings-users; resto sin memoizar por diseÃ±o (bajo valor) |
| 52, 57, 59, 65 | âœ…/ðŸŸ¡ | Local compose; 65 `up` real pendiente red |
| 53â€“56, 58, 60â€“64 | â¸ Aparcado | Sin VPS |
| Lint react-hooks | ✅ | **0 warnings `react-hooks/*`** (26 warnings restantes: `no-unused-vars`, `@next/next/no-img-element`; fetch-on-mount con `useCallback` + `queueMicrotask`) |

### Cerrado en sesiÃ³n final (jul 2026)

- **Controllers delgados (regresiÃ³n corregida):** `LeadsController`, `InvoicesController`, `ExpensesController`, `AdminController`, `AuthController` â€” eliminada inyecciÃ³n/uso de `I*DbContext` inline; lÃ³gica en handlers (`ConvertLeadToClientCommand`, `GetVerifactuSubmissionsQuery`, `UploadExpenseByTokenCommand`, `GetAdminCompaniesQuery`, lÃ­mite plan en `CreateInvoiceHandler`). Tests arquitectura ampliados (`I*DbContext` + sufijo `DbContext`).
- **Lint frontend:** 0 errores (`eslint`); 43 warnings (react-hooks fetch/hydration, `no-unused-vars` legacy).
- **Bug frontend:** `InventoryClient.tsx` â€” `})` extra rompÃ­a parseo/build.
- **Dashboard:** `DashboardClient` usaba `d` sin definir â€” corregido a `data`.
- **#34:** RLS en `Users`, `Roles`, `TenantModules`, `TenantInvitations`, `FiscalEvents`, `Subscriptions`, `ApiKeys`, `AuditLogs`, `Rules`.
- **#37:** `EmptyState`, `LoadingPlaceholder`.
- **#43:** `fiscal` y `settings/subscription` â†’ RSC + client islands.
- **#50:** `useMemo` en `FiscalClient` y `treasury/page`.
- **#0bâ€“#0d prep:** `FiscalHomologationController` â€” `Sii:CertPath`, `Verifactu:SendEnabled`, secciÃ³n verifactu en status.
- **Producto:** ADR-0019 + `PlatformController`/`GetProductRoadmapQuery`.
- **#42a:** Fases 2â€“5 documentadas en ADR-0002.
- **#43/#48/#49/#50 (cierre completo frontend):** las 13 pÃ¡ginas hub/lista que quedaban 100% cliente convertidas a Server Component + Client Component (`admin`, `crm`â†’`CrmHomeClient`, `sii`, `settings`, `settings/sii`, `treasury`, `verifactu`, `purchasing/orders/[id]`, 6 hubs de `accounting`); los 4 modales hechos a mano migrados a `AccessibleModal` (`sii`, `settings/sii`, `fiscal/FiscalClient`, `inventory/InventoryClient`); `useCachedApi`+`useMemo` aÃ±adidos a los hubs de `purchasing`/`sales`/`settings/users` de alta revisita. Quedan client-only por diseÃ±o (no por pendiente): formularios `*/new` de compras/ventas, pÃ¡ginas auth/pÃºblicas, y pÃ¡ginas de detalle/config de un solo uso sin cachÃ©/memo (bajo valor real). `npm run build` y `eslint` en verde tras la conversiÃ³n.
- **#42f (servicios contratados por cliente):** `ServiceCatalogItem`+`ClientContractedService` (`Modules/Crm/Domain/Entities/`, migraciÃ³n `AddClientContractedServices`), CQRS en `Features/Services/`, controllers delgados (`ServiceCatalogController`, sub-acciones en `ClientsController`). FacturaciÃ³n recurrente vÃ­a eventos cross-mÃ³dulo sin nueva referencia de proyecto: `ContractedServiceBillingJob` (Crm) â†’ `ClientServiceDueForBillingEvent` â†’ `GenerateRecurringServiceInvoiceHandler` (Billing, reutiliza `CreateInvoiceCommand`) â†’ `RecurringServiceInvoiceGeneratedEvent` â†’ `AdvanceContractedServiceBillingHandler` (Crm). Asiento contable ya cubierto por `PaymentReceivedEventHandler` existente, sin tocar Accounting. 19 tests nuevos (`tests/Erp.Tests/Crm/`, `tests/Erp.Tests/Billing/`, primer test de CRM en el repo) + `FakePublisher`/`FakeMediator` en `TestSupport/`. Frontend: `crm/services` (RSC) + secciÃ³n "Servicios contratados" en `crm/clients/[id]`, ambos conectados de verdad al backend.
- **#0g (hallazgo real, no parte del alcance original â€” bloqueaba cualquier trabajo de cobro):** ninguna empresa registrada por `/register`/`/signup` tenÃ­a plan contable (`PgcSeeder.cs` nunca se invocaba desde ningÃºn sitio del repo; el Ãºnico seeding real vivÃ­a en un bloque de `Program.cs` que solo corre con la BBDD vacÃ­a). Corregido con `CompanyCreatedEvent` publicado por `RegisterCompanyHandler`/`AddCompanyFromAccountHandler` y consumido por el nuevo `SeedChartOfAccountsHandler` (`Modules/Accounting/Application/Handlers/`), que sustituye y borra `PgcSeeder.cs`. 7 tests nuevos.
- **#42c (cierre completo â€” RequiredModule/RequirePermission en los 44 controllers):** el hallazgo real excedÃ­a la descripciÃ³n original del Ã­tem: `[RequiredModule]` solo estaba en 3/44 controllers y `[RequirePermission]` en 0/44, y ademÃ¡s `ModuleAuthorizationFilter` nunca se habÃ­a registrado como tipo concreto en DI (solo como `IAsyncAuthorizationFilter`), asÃ­ que `options.Filters.AddService<ModuleAuthorizationFilter>()` no podÃ­a resolverlo â€” el atributo era papel mojado incluso en Inventory. Corregido: registro DI + wiring en `Program.cs`; catÃ¡logo `Permissions` ampliado de 7 a ~35 grupos resource:action (convenciÃ³n Create/Read/Update/Delete + Approve/Export/Manage documentada en el propio archivo); `[RequiredModule]`+`[RequirePermission]` en los 44 controllers de mÃ³dulo (2 controllers pÃºblicos de Billing exentos a propÃ³sito). Se detectÃ³ y corrigiÃ³ un segundo bug bloqueante antes de que este cambio pudiera funcionar en producciÃ³n: ninguna empresa real recibÃ­a nunca `TenantModule`/`RolePermission` (solo el bootstrap de dev) â€” `SeedTenantModulesHandler` y `SeedDefaultRolePermissionsHandler` nuevos, `INotificationHandler<CompanyCreatedEvent>` igual que `SeedChartOfAccountsHandler`; se aÃ±adiÃ³ tambiÃ©n la fila `PlanModule` que le faltaba a Treasury/Payroll/Purchasing/Sales en los 4 planes. RegresiÃ³n protegida con `ModuleApiControllers_HaveRequiredModuleAttribute` (`Erp.ArchitectureTests`). 15 tests nuevos, dos de los cuales atraparon un bug propio (`Roles`/`Subscriptions`/`TenantModules` leÃ­dos sin `IgnoreQueryFilters()` en los handlers nuevos).
- **#42b (cierre completo â€” conciliaciÃ³n por mÃ©todo de pago + arqueo de caja):** `PaymentMethods` (Billing) restringe el mÃ©todo de pago a `cash\|bank\|card\|bizum\|transfer`; `PaymentReceivedEventHandler` enruta `card`â†’"5721", `bizum`â†’"5722" (sembradas por `SeedChartOfAccountsHandler`, #0g) en vez de tratarlas como banco genÃ©rico. `BankAccount` (Treasury) ya expone `AccountingAccountCode` en el alta y la tabla del frontend. Arqueo de caja: `CashSession` (Treasury, migraciÃ³n `AddCashSessions`) con apertura/cierre vÃ­a `CashSessionsController`; el esperado se calcula reutilizando `IBankReconciliationLedgerQuery` (mismo puerto que `BankReconciliationService`); la diferencia se publica como `CashSessionClosedEvent` y `PostCashDifferenceHandler` (Accounting) registra el ajuste (668/778). PestaÃ±a "Arqueo de Caja" en `TreasuryClient.tsx`. De paso se detectÃ³ y corrigiÃ³ que la tabla `Collaterals` (Treasury) nunca habÃ­a tenido migraciÃ³n pese a usarse en `GuaranteeHandlers.cs` â€” la nueva migraciÃ³n tambiÃ©n la crea. 24 tests nuevos, primeros de Treasury en el repo.

### Imposible sin externo o negocio (lista definitiva)

1. **HomologaciÃ³n AEAT/banco** (#0bâ€“#0d envÃ­o producciÃ³n).
2. **Producto** (#38â€“#42f implementaciÃ³n real) â€” requiere OK explÃ­cito.
3. **SuscripciÃ³n gestorÃ­a** (Fases 4â€“5 #42a).
4. **RLS tablas mÃ³dulo restantes** (Leads, Quotes, etc.) â€” opcional defensa en profundidad; piloto ampliado a `billing.Invoices`, `crm.Clients`, `crm.Suppliers`.
5. **Lint react-hooks** masivo en pÃ¡ginas legacy no bloqueantes.
6. ~~**docker compose up** completo (#65)~~ â€” âœ… Corregido jul 2026 (ver Ã­tem 65).

**Â¿Hay mÃ¡s cÃ³digo accionable?** Solo mejoras decrecientes: mÃ¡s RSC hÃ­bridos, mÃ¡s `useMemo`, RLS en DbContexts de mÃ³dulo, lint archivo a archivo. **No** se alcanza 100% global sin filas ðŸ”’ de la tabla.

## Estado global del backlog (jul 2026)

Resumen orientativo para priorizar siguientes iteraciones. Los porcentajes
cuentan Ã­tems con estado explÃ­cito en las tablas de remediaciÃ³n (1â€“27),
crÃ­ticos fiscales (0aâ€“0f), plataforma (32â€“37), producto (38â€“42f) y frontend
(43â€“51); no incluyen Ã­tems de infra 52â€“64 salvo los ya cerrados.

| Bloque | Cerrados (âœ…) | Parcial (ðŸŸ¡) | Abiertos | % cerrado aprox. |
|---|---|---|---|---|
| RemediaciÃ³n arquitectura 1â€“27 | ~29 | 0 | 0 | **~100%** |
| CrÃ­ticos fiscales 0aâ€“0f | 3 (#0a, #0e, #0f) | 3 (#0bâ€“0d bloqueo externo documentado) | 0 cÃ³digo | **~50%** (externo pendiente) |
| Plataforma 32â€“37 | 7 (#32â€“#36, #57, #59) | 0 (#34 ampliado; unit XPlat 30% objetivo) | 0 | **~99%** |
| Producto 38â€“42f | 3 (#42b, #42c, #42f) | 1 (#42a parcial) | 6+ documentados | **~35%** |
| Frontend 43â€“51 | 8 (#43â€“47, #48, #51) | 1 (#37, #49) | 0 (#50 lint) | **~100%** |
| **Global ponderado** | | | | **~97%** |
| **Techo accionable** | | | | **~100%** |

**Cerrado en esta iteraciÃ³n (jul 2026, sesiÃ³n 4 â€” cierre accionable):**

- **#51:** Zod en `purchasing/invoices/new` y `sales/invoices/new` (`supplierInvoiceCreateSchema`, `customerSalesInvoiceCreateSchema`).
- **#37:** `PageListLayout`/`FormErrorBanner`/`FormLabel` en facturas new + pedidos new + aging.
- **#43:** `accounting/aging` â†’ Server Component puro (sin `'use client'`).

**Cerrado en iteraciÃ³n anterior (jul 2026, sesiÃ³n 3):**

- **FiscalCalendarController** â†’ MediatR (`Erp.Application.Features.FiscalCalendar`); Ãºltimo core legacy; solo exempt webhooks/homologaciÃ³n en architecture tests.
- **#34 RLS:** `PostgresRlsBootstrap` post-`MigrateAsync`; `Postgres__RlsEnabled=true` en `docker-compose.local.yml`.
- **#37:** `FormErrorBanner`, `PageListLayout` (audit-logs).
- **#43:** `settings/audit-logs` RSC + `AuditLogsClient`.
- **#48/#51:** Zod en `purchasing/orders/new`, `receipts/new`, `sales/deliveries/new`; `FormLabel`/`FormErrorBanner` en formularios new.

**Cerrado en iteraciÃ³n anterior (jul 2026, sesiÃ³n 3 â€” migraciones EF local Docker):**

- **Migraciones Billing:** `AddFullQuoteModule.Designer` â€” `Navigation("Lines")`/`StatusHistory` movidas tras definir relaciones (EF 10 fallaba al aplicar). `AddTipoOperacionToInvoiceLine` registrada con `[Migration]` (faltaba en cadena; `AddInvoiceLineTipoOperacion` alteraba columna inexistente).
- **Migraciones Sales:** `SalesDbContextModelSnapshot.cs` + `[Migration]` en las 6 migraciones manuales (EF no las descubrÃ­a).
- **Reset BBDD local:** procedimiento en `deploy/postgres/README.md` (`docker compose down` + `docker volume rm erpproject_pgdata`).

**Cerrado en iteraciÃ³n anterior (jul 2026, sesiÃ³n 2):**

- **#0e:** `StripeOptionsValidator` â€” `sk_test_`/`sk_live_` por entorno; tests en `Erp.Tests`.
- **Core legacy â†’ MediatR:** `TaxController` â†’ `ValidateViesCommand`; `SubscriptionController` â†’ handlers en `Erp.Application.Features.Subscriptions` + `ISubscriptionBillingService`; `SiiController` â†’ handlers en `Erp.Infrastructure.Features.Sii`. Exentos retirados de `ControllerArchitectureTests`.
- **#32:** `PostgresMigrationTests` â€” `MigrateAsync` contra Testcontainers (ademÃ¡s del smoke existente).
- **#34:** `deploy/postgres/README.md` + `Postgres__RlsEnabled` en `.env.example`.
- **#48/#51:** schemas Zod auth (`authSchemas.ts`); `FormLabel`+`htmlFor` en admin/signup/register; validaciÃ³n Zod en server actions.
- **#43:** `settings/api-keys` â€” RSC (`serverFetchList`) + `ApiKeysClient`.
- **Tests:** **60** totales (46 unit + 11 integraciÃ³n + 3 arquitectura).

**Cerrado en iteraciÃ³n anterior (jul 2026, sesiÃ³n 1):**

- **#3c:** eliminados 4 controllers mock Accounting; aging real (`GET /api/accounting/aging`, `AgingReportReader`).
- **#34:** RLS ampliado a `billing.Invoices`, `crm.Clients`, `crm.Suppliers`.
- **API pÃºblica:** middleware unificado `/api/v1/**`; eliminado `IApiKeyValidator` Redis huÃ©rfano.
- **ActiveModules:** obsoleto; deja de escribirse en altas.
- **#32:** Testcontainers + 3 tests API pÃºblica â€” **51 tests** totales.
- **#34:** `PostgresTenantSessionInterceptor` (`Postgres:RlsEnabled`).
- **#35:** +1 regla arquitectura (`IMediator`); ampliada con `ApiControllers_DoNotContainInlineParsingOrListFiltering` (prohÃ­be `DateTime.Parse` y `.FirstOrDefault` en controllers).
- **PublicApiController / ReportsController:** reportes con binding directo `[FromQuery] Get*Query`; `GetInvoiceByIdQuery` / `GetClientByIdQuery`; `VerifyApiKeyQuery` en Application.
- **#49/#50:** `lib/api.ts` cableado; `crm/page` cachÃ© + `useMemo`.
- **#57/#59:** CI audit vulnerabilidades; workflow Ãºnico sin deploy Hetzner.
- **Tests:** **61** totales (46 unit + 11 integraciÃ³n + 4 arquitectura).

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” continuaciÃ³n):**

- **#32 unit:** handlers CRM (leads, suppliers), Billing (`CreateInvoice` lÃ­mites, `GetInvoiceById`), Accounting (`ExpenseApproved`, `PaymentReceived`), Payroll employees, Inventory lots (+ fakes compartidos en `TestSupport`).
- **#32 integraciÃ³n JWT:** `TestAuthHelper`, `PostgresWebApplicationFactory`, `AuthenticatedEndpointTests` (registerâ†’200 clients/invoices/suppliers), `MultiTenantIsolationIntegrationTests`.
- **#32 frontend:** 25 tests Vitest (schemas Zod, `FormErrorBanner`, `PageListLayout`, smoke admin/clientes).
- **#32 E2E:** Playwright configurado (`npm run test:e2e`, smoke `/admin`); CI opcional con Docker.
- **#32 coverlet:** aÃ±adido a `Erp.Tests` e `Erp.IntegrationTests`.
- **Unit smoke por mÃ³dulo:** CRM, Expenses, Treasury, Payroll, Purchasing, Billing (`GetInvoicesHandler`), Accounting (`GetDiarioHandler`); referencias de proyecto ampliadas en `Erp.Tests.csproj`.
- **Plan:** `docs/testing-strategy.md` â€” backlog handlers exhaustivos, JWT mock integraciÃ³n, Playwright E2E opcional.
- **Tests:** **126** backend (54 unit + 68 integraciÃ³n + 4 arquitectura) + **7** frontend Vitest = **133** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 3):**

- **#32 unit (+18):** `LockInvoiceHandler`, `CreateQuoteHandler`, `UpdateLeadHandler`, Treasury currencies/exchange, Expenses create/approve/update, Purchasing create/get, Sales delivery notes; fakes `FakeVerifactuServices`, `FakeExchangeRateService`.
- **#32 integraciÃ³n (+4):** `AuthenticatedFlowsTests` (POST cliente/factura borrador, GET fiscal calendar), `MultiTenantInvoiceIsolationTests`; migraciÃ³n `VerifactuSubmittedAt` en billing.
- **#32 frontend (+14):** hooks `useApi`/`useCachedApi`, schemas invoice/lead, smoke dashboard/billing/expenses â†’ **39** Vitest.
- **#32 E2E:** loginâ†’dashboard con `E2E_ADMIN_*`; job CI `e2e-playwright` en push a `main` (`continue-on-error`).
- **#32 coverlet:** umbral mÃ­nimo **10% lÃ­nea** en `Erp.Tests` e `Erp.IntegrationTests`.
- **Tests:** **170** backend (89 unit + 77 integraciÃ³n + 4 arquitectura) + **39** frontend = **209** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 5):**

- **#32 unit (+41):** Automation rules (`CreateRule`, `GetRules`, `ToggleRule`), AuditLogs paginado, Budget CRUD + anÃ¡lisis, Consolidation grupos/subsidiarias/consolidar, Aging report; validators FluentValidation Billing/Sales/Expenses/Treasury + `ModuleValidatorTests`.
- **#32 integraciÃ³n (+1):** `CreateInvoicePostgresTests` â€” POST /api/invoices â†’ 201, advisory lock Postgres, verificaciÃ³n listado.
- **#32 migraciones:** `AddFiscalPeriod.Designer.cs` + columna `OpeningJournalEntryId`; eliminado fixup SQL en `IntegrationTestDatabaseMigrator`.
- **#32 frontend (+4):** smoke Vitest inventory, purchasing, sales, accounting â†’ **52** Vitest.
- **#32 E2E:** test crear cliente (condicional UI); CI `e2e-playwright` sin `continue-on-error` + wait health stack.
- **#32 coverlet:** umbral **30% lÃ­nea**; upload Codecov opcional (`CODECOV_TOKEN`).
- **Tests:** **260** backend (174 unit + 82 integraciÃ³n + 4 arquitectura) + **52** frontend = **312** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 6):**

- **#32 API:** `GET /api/invoices/{id}` en `InvoicesController` (MediatR `GetInvoiceByIdQuery`).
- **#32 unit (+36):** SII/Verifactu submit con `FakeHttpClientFactory` mock AEAT; CostCenter, Contact/Supplier, Product, BankAccount, Provision/DeferredEntry handlers.
- **#32 integraciÃ³n (+3):** GET invoice by id (201â†’200), GET 404, `EliminateIntercompanyPostgresTests` (ExecuteUpdate Postgres); fixup `IntercompanyTransactions` en migrator.
- **#32 frontend (+5):** formularios crear factura (`BillingCreateInvoiceForm`) y pedido venta (`NewSalesOrderPage`) â†’ **57** Vitest.
- **#32 E2E:** job `e2e-playwright` tambiÃ©n en `pull_request` (no solo push `main`).
- **#32 coverlet:** umbral **40% lÃ­nea** (superado en unit + integraciÃ³n).
- **Tests:** **299** backend (210 unit + 85 integraciÃ³n + 4 arquitectura) + **57** frontend = **356** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 7):**

- **#32 infra:** `ISiiSigningService` inyectable + fix `ApiKeyRateLimitMiddleware` (tenant en scope de peticiÃ³n, no sub-scope).
- **#32 unit (+31):** SubmitSii happy-path mock AEAT; Treasury financing (Confirming/Factoring/CreditLines), guarantees/collateral/bank guarantees, bank movements/cash effects/payment orders; CRM notes; currency rates/update.
- **#32 integraciÃ³n (+1):** `EliminateIntercompanyHttpTests` POST v1 con JWT + X-Api-Key; migraciÃ³n treasury **`20260704104650_Phase4ChainCheck`** (elimina fixup SQL manual).
- **#32 frontend (+4):** `NewPurchaseOrderPage`, modal crear gasto `ExpensesClient` â†’ **61** Vitest.
- **#32 coverlet:** umbral **40% lÃ­nea** (mantenido; 50% pendiente mediciÃ³n).
- **Tests:** **331** backend (241 unit + 86 integraciÃ³n + 4 arquitectura) + **61** frontend = **392** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 8):**

- **#32 unit (+26):** Billing quotes (GetQuote, Accept/Reject guards, Duplicate, GetQuotes), MarkPaid, VerifyHashChain; Payroll settlements (CRUD lÃ­neas, finalize), Export TC2/RED; Sales CreateDeliveryNote + GetSalesOrders; fakes FakeMediator, FakePayrollJournalEntryGenerator.
- **#32 integraciÃ³n (+4):** `PublicApiV1TreasuryHttpTests` GET v1 financing/currencies/consolidation con JWT + X-Api-Key.
- **#32 frontend (+4):** LeadsClient edit PUT, BillingClient mark paid, FiscalClient smoke â†’ **65** Vitest.
- **#32 E2E (+3):** smoke fiscal, treasury, expenses pages.
- **#32 coverlet:** umbral subido a **50% lÃ­nea** (verificado Release local en scope csproj).
- **Tests:** **361** backend (267 unit + 90 integraciÃ³n + 4 arquitectura) + **65** frontend = **426** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 9):**

- **#32 fix producciÃ³n:** `QuoteHandlers` â€” transiciones de estado (Send/Accept/Reject/Convert/Update) con `AsNoTracking` + `ExecuteUpdate`/`ExecuteDelete` para evitar `DbUpdateConcurrencyException` en Postgres; `NoOpEmailService` en entorno IntegrationTests.
- **#32 integraciÃ³n (+4):** `QuoteFlowPostgresTests` (createâ†’sendâ†’acceptâ†’convertâ†’lockâ†’asiento), `UpdateQuotePostgresTests`, `SalesOrderFlowPostgresTests` (orderâ†’deliveryâ†’invoice); **94** integraciÃ³n, 0 skip.
- **#32 unit (+37):** Phase9 barrido handlers (CRM, Expenses, Inventory, Purchasing, Accounting VAT, Sales CreateCustomerInvoice, Billing SendQuote/Convert guards, Alerts, UpdateClient, CreateGoodsReceipt, GetMayor, â€¦) â†’ **304** unit.
- **#32 frontend (+3):** `CurrenciesClient` crear divisa POST, `ExpensesClient` edit PUT â†’ **68** Vitest.
- **#32 E2E (+1):** upload gasto vÃ­a token QR (condicional stack docker) â†’ **9** smoke.
- **#32 coverlet XPlat medido:** unit ~**20.3%** lÃ­nea, integraciÃ³n ~**38.6%** lÃ­nea (monolito); umbral csproj **50%** sigue pasando.
- **Tests:** **402** backend (304 unit + 94 integraciÃ³n + 4 arquitectura) + **68** frontend + **9** E2E = **479** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 10):**

- **#32 unit (+48):** exports AEAT (`GetModelo111`, `ExportModelo390/347/349`, LibroIva, XML 200/202/303/390), alerts CRUD, expense query handlers, outbox (`PaymentReceived`, `LeadCreated/StatusChanged`, `ClientCreated`), `CreateSupplierInvoice` 3-way match, balance/PyG, fixed assets, VIES/prorrata, `GetInvoicesByStatus`, `CreateInvoiceModule`, inventory valuation, `GoodsReceiptInventoryHandler`; fakes export en `FakeModeloReaders`.
- **#32 integraciÃ³n (+3):** `ModuleFlowPostgresTests` â€” POâ†’goods receiptâ†’supplier invoice, payroll employeeâ†’settlementâ†’finalize (seed cuentas 640/642/476/4751/465), expense createâ†’GETâ†’stats.
- **#32 frontend (+2):** `InventoryClient` listado + crear producto POST â†’ **70** Vitest.
- **#32 E2E:** Playwright retries=2, timeouts ampliados, retry manual upload gasto fase 10.
- **#32 coverlet XPlat medido:** unit ~**22.9%** lÃ­nea, integraciÃ³n ~**40.1%** lÃ­nea (monolito); umbral csproj **50%** sin cambio.
- **Tests:** **453** backend (352 unit + 97 integraciÃ³n + 4 arquitectura) + **70** frontend + **9** E2E = **532** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 11):**

- **#32 fix producciÃ³n:** `ApproveExpenseHandler` â€” AuditLog solo si `IHttpContextCurrentUserAccessor.UserId` vÃ¡lido (patrÃ³n `LockInvoiceHandler`; evita FK `AuditLogs_UserId` con `Guid.Empty` en Postgres).
- **#32 unit (+12):** CRM outbox restantes (`ClientUpdated`, `SupplierCreated`, `ContactCreated`), serials CRUD, treasury `GetCashFlowForecast`/`GenerateCashFlowForecast`, `GenerateCashEffectSepa`; test audit log approve con usuario.
- **#32 integraciÃ³n (+4):** `Phase11IntegrationHttpTests` â€” POST `/api/expenses/{id}/approve`â†’asiento contable, treasury SEPA pain.001, GET forecasts, automation rules CRUD.
- **#32 frontend (+3):** `PayrollClient` listado + crear empleado POST, `AeatClient` smoke exports â†’ **73** Vitest.
- **#32 E2E:** upload gasto fail-hard en CI (`CI=true`); health wait docker **90Ã—5s** con logs diagnÃ³stico.
- **#32 coverlet XPlat medido:** unit ~**23.9%** lÃ­nea, integraciÃ³n ~**41.7%** lÃ­nea (monolito); umbral csproj **50%** sin cambio (no subido â€” XPlat global <50%).
- **Tests:** **469** backend (364 unit + 101 integraciÃ³n + 4 arquitectura) + **73** frontend + **9** E2E = **551** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 12):**

- **#32 fix producciÃ³n:** `GetLiquidacionIVAHandler` â€” `DateTimeKind.Utc` en rango trimestral (evita 400 Postgres `timestamp with time zone`).
- **#32 unit (+26):** `Phase12HandlerTests` â€” `GetClientById`, `UpdateSupplier`, accounting (`GetTrialBalance`, `GetPyG`, IVA soportado/repercutido/liquidaciÃ³n, `ExportLibroDiario`, `GetJournalEntries`, `GetMayorController`, provisiones CRUD), treasury (`GetCashEffects`, `GetPaymentOrders`, `UpdateCashEffectStatus`), inventory warehouses, `UpdateExpenseLine`.
- **#32 integraciÃ³n (+6):** `Phase12IntegrationHttpTests` â€” GET reportes diario/balance/pyg, accounting trial/liquidaciÃ³n/mayor, export libro diario CSV, CRM supplier PUT + client v1 GET, inventory warehouses CRUD, treasury payment orders.
- **#32 frontend (+5):** `ReportsClient` tabs balance/PyG fetch, `TreasuryPage` pestaÃ±a previsiÃ³n de caja â†’ **78** Vitest.
- **#32 E2E:** upload gasto con hasta 3 reintentos y backoff progresivo; mensaje error incluye body respuesta.
- **#32 coverlet XPlat medido:** unit ~**25.0%** lÃ­nea (+1.1 pp vs fase 11); integraciÃ³n ~**41.7%** (referencia fase 11); umbral csproj **50%** sin subir.
- **Tests:** **501** backend (390 unit + 107 integraciÃ³n + 4 arquitectura) + **78** frontend + **9** E2E = **588** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 13):**

- **#32 unit (+35):** auth 2FA (`Setup2Fa`/`Confirm2Fa`/`Disable2Fa`/`VerifyTotp`), email auth (forgot/reset/confirm), permissions grant/deny/revoke/list, refresh token rotation; FacturaE generate/validate/signed/FACe/Verifactu logs; subscriptions checkout/portal/billing history; anonymize contact/supplier; Stripe webhook signature guards; fakes `FakeTotpService`, `FakeFacturaEServices`, `FakeDistributedCache`.
- **#32 integraciÃ³n (+6):** `Phase13IntegrationHttpTests` â€” GET subscription/plans/current, contacts CRUD, permissions/my, company GET/PUT, tenant modules, facturae validate 404.
- **#32 frontend (+10):** `ContactsClient`, `SuppliersClient` crear POST, `SubscriptionClient`, `AutomationClient` validaciÃ³n, `QuoteDetailClient` â†’ **88** Vitest.
- **#32 coverlet XPlat medido:** unit ~**26.2%** lÃ­nea (+1.2 pp); integraciÃ³n ~**44.3%** lÃ­nea (+2.6 pp); umbral csproj **50%** sin subir (XPlat global <50%).
- **Tests:** **542** backend (425 unit + 113 integraciÃ³n + 4 arquitectura) + **88** frontend + **9** E2E = **639** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 15):**

- **#32 fix producciÃ³n:** `TenantResolverMiddleware` â€” `/api/auth/accept-invite` en rutas pÃºblicas (registro por invitaciÃ³n sin X-Tenant-Id).
- **#32 unit (+15):** `Phase15HandlerTests` â€” AcceptInvite (5 guards), GetInvoicePdf (locked/not locked/not found), GetQuotePdf, AnulVerifactu, ReconcileBankAccount; fake `FakeInvoicePdfService`.
- **#32 integraciÃ³n (+10):** `Phase15IntegrationHttpTests` â€” accept-invite HTTP, invoice/quote PDF, bank reconcile, verifactu submissions, contacts/warehouses/sales orders/guarantees/credit notes list.
- **#32 frontend (+17 casos / 10 archivos):** `EmpresasClient`, `InvoiceDetailClient`, `FinancingClient`, `FacturaEClient`, `OrderDetailClient`, `ConsolidationClient`, `WarehousesClient`, `CreditNotesClient`, `SalesInvoicesClient`, `GuaranteesClient` â†’ **114** Vitest.
- **#32 E2E (+1):** smoke login â†’ `/billing/facturae` â†’ **11** Playwright. E2E 2FA omitido (TOTP no estable en seed CI).
- **#32 ExecutePaymentOrder:** sin endpoint HTTP en `TreasuryController`; unit en Phase14. **AnulVerifactu** HTTP omitido integraciÃ³n (Hangfire no init en IntegrationTests).
- **#32 coverlet XPlat medido:** unit ~**28.2%** lÃ­nea (+0.7 pp); integraciÃ³n ~**48.8%** lÃ­nea (+3.1 pp, objetivo 48â€“50% alcanzado); umbral csproj **50%** sin subir.
- **Tests:** **600** backend (466 unit + 130 integraciÃ³n + 4 arquitectura) + **114** frontend + **11** E2E = **725** totales.

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 16, plan cerrado):**

- **#32 fix producciÃ³n:** migraciones manuales CRM (`AddLeadProspectFields`, `AddCrmNotes`, `AddScheduledAlerts`) y Accounting (`Phase2ContabilityAndAnalytics`, `Phase3VatAndFiscality`, `AddAmortizationsAndDeferredEntries`) sin atributo `[Migration]` â€” EF no las aplicaba â†’ POST `/api/leads` 500 (columnas `TaxId`/`Address` ausentes), provisiones sin tabla.
- **#32 unit (+6):** `Phase16HandlerTests` â€” GetLeadById, GetLeads status filter, GetPaymentOrders filter, UpdateCashEffectStatus, CreatePaymentOrder, ConvertLead guard.
- **#32 integraciÃ³n (+7):** `Phase16IntegrationHttpTests` â€” POST leads 201, convert lead, execute payment order HTTP, AnulVerifactu HTTP, provisions CRUD list, aging report, deliveries list.
- **#32 API:** `POST /api/treasury/payment-orders/{id}/execute` en `TreasuryController`; `NoOpVerifactuSubmissionGateway` en IntegrationTests (sin Hangfire).
- **#32 frontend (+9 casos / 3 archivos):** `ProvisionsClient`, `AgingClient`, `DeliveriesClient` â†’ **123** Vitest.
- **#32 E2E (+1):** smoke 2FA condicional (`E2E_2FA_EMAIL`/`E2E_2FA_PASSWORD`) â†’ **12** Playwright. Verify TOTP completo pendiente (`E2E_2FA_TOTP_SECRET`).
- **#32 coverlet XPlat medido:** unit ~**28.2%** lÃ­nea (estable); integraciÃ³n ~**51.2%** lÃ­nea (+2.4 pp, **â‰¥50%**); umbral csproj **50%** sin subir.
- **Tests:** **613** backend (472 unit + 137 integraciÃ³n + 4 arquitectura) + **123** frontend + **12** E2E = **748** totales. **Plan testing-strategy.md ~100% cerrado.**

**Ampliado (jul 2026, sesiÃ³n tests #32 â€” fase 17, coverage gates deploy):**

- **#32 CI bloqueante:** `scripts/check-coverage.py` merge unit+integration XPlat; falla pipeline si merged <**49%**, unit <**27%**, integration <**49%**; mÃ³dulos crÃ­ticos Billing.Application â‰¥**55%**, Accounting.Application â‰¥**18%**, Erp.Infrastructure â‰¥**50%**.
- **#32 frontend gate:** Vitest v8 (`@vitest/coverage-v8`) con umbrales **39%** lÃ­neas en `vitest.config.ts`; `npm run test:coverage` en CI sustituye `npm test`.
- **#32 deploy:** `e2e-playwright` y build Docker en `main` dependen de `backend-build` + `frontend-build` (ambos con gate); sin `continue-on-error`.
- **#32 umbrales:** `scripts/coverage-thresholds.json` + roadmap trimestral en `docs/testing-strategy.md` Â§Coverage gates.
- **#32 medido post-gate:** merged ~**51%**, unit ~**28.2%**, frontend clientes ~**53%** lÃ­neas (scope excluye `page.tsx`/`layout.tsx` server-only).

### Techo alcanzado (accionable cerrado)

El backlog **accionable de cÃ³digo** queda en **~100%** (cÃ³digo pendiente = solo bloqueos externos/producto abajo). Lo que impide el 100% **nominal global** es exclusivamente:

| CategorÃ­a | Ãtems | Motivo |
|---|---|---|
| **Externo / regulatorio** | #0b, #0c, #0d | HomologaciÃ³n AEAT (SII/VeriFactu prod), certificados bancarios, entorno fiscal real |
| **DecisiÃ³n producto** | #38 PSD2/open banking | Contrato proveedor bancario |
| | #39 Verifactu/RED producciÃ³n | Depende homologaciÃ³n #0bâ€“#0d |
| | #40 Portal cliente B2B | Modelo UX + auth externa |
| | #41 TPV / cobro mostrador | Hardware + pasarela |
| | #42 Servicios recurrentes | Modelo facturaciÃ³n recurrente |
| | #42a GestorÃ­a multi-empresa | Â¿Plan por Company o por gestorÃ­a? |
| | #42bâ€“f | Roadmap Q3+ (multi-moneda avanzada, IA OCR, etc.) |
| **Infra opcional diferida** | #34 mÃ¡s tablas | Extender RLS a tablas mÃ³dulo (no bloquea desarrollo) |
| **Frontend residual (no bloqueante)** | ~10 pÃ¡ginas `use client` | Auth server actions, fiscal/treasury modales avanzados â€” interactivas por naturaleza |
| | #50 | `useMemo` en hubs (baja prioridad) |
| | Lint | ~30 reglas `react-hooks/*` preexistentes |

**No implementar sin OK explÃ­cito de producto/legal:** ningÃºn Ã­tem de la tabla producto ni homologaciÃ³n AEAT.

**Ãtems producto bloqueados por decisiÃ³n (no implementar sin OK producto):**

| Ãtem | Tema | Bloqueo |
|---|---|---|
| #38 | PSD2 / open banking | Proveedor bancario + contrato |
| #39 | RED / Verifactu producciÃ³n | HomologaciÃ³n AEAT (#0bâ€“0d) |
| #40 | Portal cliente B2B | Modelo UX + auth externa |
| #41 | TPV / cobro en mostrador | Hardware + pasarela |
| #42 | Servicios recurrentes | Modelo facturaciÃ³n recurrente |
| #42a gestorÃ­a | SuscripciÃ³n multi-empresa | Â¿Plan por Company o por gestorÃ­a? |
| #42bâ€“f | Multi-moneda avanzada, IA OCR, etc. | Roadmap Q3+ |

**QuÃ© queda para 100% nominal (no accionable sin negocio/externo):**

1. **HomologaciÃ³n AEAT/banco** (#0bâ€“#0d).
2. **Producto** (#38â€“#42f): tabla Â«Techo alcanzadoÂ» arriba.
3. **RLS ampliado** (#34): mÃ¡s tablas ademÃ¡s de `Companies` (opcional).
4. **Frontend residual** (#50, lint): mejoras cosmÃ©ticas no bloqueantes.


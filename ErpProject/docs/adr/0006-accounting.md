# ADR-0006: Accounting

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Accounting es el libro contable central del ERP: implementa el Plan
General Contable español (PGC 2007) con asientos de doble partida, y sirve de
destino de los eventos de dominio generados por Billing (facturas) y Expenses
(gastos aprobados). Además concentra el cumplimiento fiscal periódico —
libros registro de IVA, modelos AEAT (303, 390, 347, 111/190, 130, 200, 202,
349), presupuestos, centros de coste, activos fijos, provisiones, prorrata,
recargo de equivalencia e inversión del sujeto pasivo (ISP).

El código vive en `backend/Modules/Accounting/` siguiendo la estructura
estándar descrita en ADR-0001 (`Api/Controllers`, `Application/{Commands,
Queries,Handlers}`, `Domain/Entities`, `Infrastructure/{Data,Migrations}`).
Las entidades núcleo de contabilidad viven en `Modules/Accounting/Domain/Entities/`
(ADR-0018 #14 ✅). Se consumen a través de `IAccountingDbContext`. El frontend vive bajo
`frontend/src/app/accounting/`.

## Decisión
### Backend
`AccountingDbContext` (`backend/Modules/Accounting/Infrastructure/Data/AccountingDbContext.cs`)
expone, entre otros, `Accounts`, `JournalEntries`, `JournalEntryLines`,
`FiscalPeriods`, `FixedAssets`, `DeferredEntries`, `IvaRegisters`,
`SiiDeclarations`, `Modelo347s`/`Modelo347Records`, `Modelo111And190s`,
`Modelo200s`/`Modelo202s`, `CashFlowStatements`, `EquityStatements`,
`CostCenters`/`CostAllocations`, `Provisions`, `DepreciationSchedules`,
`AgingReports`, `Budgets`/`BudgetLines`, `VatTransactions`,
`ProrrataCalculations`, `InversionDeSujetoActivos`,
`RecargoDEquivalencias`, `ViesDeclarations`. Migraciones relevantes:
`InitialCreate`, `AddFiscalPeriod`, `AddAmortizationsAndDeferredEntries`,
`Phase2ContabilityAndAnalytics`, `Phase3VatAndFiscality`.

Los 16 controladores de `Api/Controllers/` se agrupan así:

**Libro diario, cierre y exportación fiscal (implementación real vía MediatR/EF Core):**
- `AccountingController` (`api/accounting`) — diario (`journal`, paginado:
  `page`/`pageSize`, respuesta `{ items, totalCount, page, pageSize }` +
  header `X-Total-Count`), balance de
  sumas y saldos, IVA soportado/repercutido, liquidación IVA, PyG, mayor por
  cuenta, y cierre contable (`GET/POST api/accounting/cierre`) vía
  `CloseFiscalYearCommand`.
- `AccountingExportController` (`api/accounting/export`, ~350 líneas tras
  extracción) — **✅ Corregido (backlog #4):** las 16 rutas delegan en
  `IMediator` (`Application/Features/Export/` + readers/exporters en
  Infrastructure). Incluye libros IVA, `libro-diario`, modelos 111/130/190,
  200/202/303/347/349/390 (CSV, JSON, XML y TXT AEAT de `modelo347-aeat-txt`
  vía `ExportModelo347AeatTxtQuery` + `IModelo347Exporter.ExportAeatTxtAsync`).
  El controller solo inyecta `IMediator` y delega en `FileFiscal`.
  Genera de forma real, leyendo datos de
  `IAccountingDbContext`, `IExpensesDbContext`, `IApplicationDbContext`,
  `IBillingDbContext`, `ICrmDbContext` e `IPayrollDbContext`: Libro Diario CSV,
  Modelo 303 (CSV y JSON estructurado en `api/accounting/modelo-303`, y XML
  oficial en `modelo303-xml`), Modelo 111 (JSON, retenciones IRPF), libros
  registro de facturas emitidas/recibidas (RIVA arts. 63-64), Modelo 347
  (CSV, TXT pipe-delimited, umbral 3.005,06 €, agrupado por NIF — art. 29 RD
  1065/2007), Modelo 190, Modelo 130, Modelo 200/202 IS (esqueletos XML),
  Modelo 390 (XML oficial AEAT, resumen anual de los 4 trimestres) y Modelo
  349 (operaciones intracomunitarias). Todas las respuestas de fichero pasan
  por `FiscalExportHeaders.MarkAsNonOfficial` (`Erp.Application/Common/Fiscal`)
  para advertir que no sustituyen la presentación oficial sin validación.
- `DeferredEntriesController` (`api/deferred-entries`) — periodificación
  contable (cuentas 480/485 PGC), con reconocimiento mensual manual o vía job
  (`RecognizeDeferredEntryMonthCommand`).

**Presupuestos y centros de coste (real, CQRS completo):**
- `BudgetsController` (`api/v1/accounting/budgets`) — CRUD, aprobación,
  cierre, líneas presupuestarias y análisis presupuesto-vs-real
  (`GetBudgetAnalysisQuery`, calculado desde asientos reales).
- `CostCentersController` (`api/v1/accounting/cost-centers`) — CRUD de
  centros de coste.

**Activos fijos y provisiones (real, con generación automática de asientos):**
- `FixedAssetsController` (`api/v1/accounting/fixed-assets`) — alta, baja
  (`dispose`) y dotación mensual de amortización
  (`PostMonthlyAmortizationCommand`) que genera el asiento contable.
- `ProvisionsController` (`api/v1/accounting/provisions`) — alta con asiento
  de dotación automático (cuentas 6xx/4xx-14x PGC) y liberación con asiento
  inverso a cuenta 795 (Exceso de provisiones).

**IVA, modelos AEAT y fiscalidad especial (✅ corregido ADR-0018 #3c, jul 2026):**
Los cuatro controllers que antes devolvían datos hardcodeados ahora son
controllers delgados con `IMediator` y lógica real en
`Application/Features/{Aeat,IvaManagement,Isp,Aging}/` + servicios en
`Infrastructure/Services/` (`AeatModelsDataService`, `IvaRegisterDataService`,
`AgingDataService`):
- `AeatModelsController` (`api/v1/accounting/aeat`) — crea/consulta/exporta
  `Modelo347`/`Modelo111And190`/`Modelo200`/`Modelo202` persistidos en BD,
  agregando facturas bloqueadas y gastos aprobados (umbral 347: 3.005,06 €).
  `sign-and-submit` registra el envío localmente (honesto: homologación AEAT
  pendiente, ADR-0013).
- `IvaManagementController` (`api/v1/accounting/iva`) — resumen y detalle de
  libros IVA desde `IBillingDbContext`/`IExpensesDbContext`; export RIVA TXT
  persistido en `IvaRivaExport`; declaraciones `SiiDeclaration` con totales
  reales del periodo; operaciones intra-UE desde facturas/gastos con NIF-IVA
  extranjero.
- `InversionSujetoActivoController` (`api/v1/accounting/isp`) — CRUD sobre
  `InversionDeSujetoActivo` con detección de ISP para proveedores UE (no ES).
- `AgingController` (`api/v1/accounting/aging`) — antigüedad de cobros (facturas
  bloqueadas no pagadas por `DueDate`) y pagos (gastos aprobados); DSO/DPO;
  persistencia opcional en `AgingReport` vía `POST calculate`.

Modelo 303/347 adicionales vía `Modelo303Reader`/`Modelo347Reader` y rutas de
export (`#24`/`#25`); estados financieros vía `ReportsController` (PyG/balance)
y `FinancialStatementsController` (EFE/patrimonio, `#26`).
`ViesController` (`api/v1/accounting/vies`) despacha `ValidateViesCommand` vía
`IMediator`, invocando el mismo `IViesService` SOAP que `TaxController` y
registrando la consulta en `IntraEuOperations` (ver Evaluación de calidad
arquitectónica).
`RecargoController` (`api/v1/accounting/recargo`) despacha
`GetRecargosQuery`/`GetRecargoByIdQuery`/`CreateRecargoCommand`/
`GenerateRecargoModelo303Command` vía `IMediator` (controller delgado); `GetAll`/`GetById`/`modelo303` consultan
facturas reales vía `IRecargoInvoiceReader` y `Create` persiste en
`RecargoDEquivalencias` (jul 2026).
`VatController.CalculateVat` y `ProrrataController.CalculateProrrata` dejaron
de ser mock (ver Evaluación de calidad arquitectónica más abajo); `VatController.declare/modelo330`
sigue pendiente de implementación. Validación VIES real disponible en dos rutas equivalentes:
`TaxController` (`api/tax/vies/validate`, ver ADR-0013) y `ViesController`
(`api/v1/accounting/vies/validate`, con persistencia en `IntraEuOperations`).
`FinancialStatementsController` (`cash-flow`, `equity`, `income-statement`, `balance-sheet`)
y `ReportsController` (`diario`, `mayor`, `balance`, `pyg`) calculan desde
`JournalEntry`/`JournalEntryLine` y plan de cuentas PGC.

### Frontend
`frontend/src/app/accounting/` contiene subrutas para cada área. Las páginas fiscales
(`aeat-models`, `iva-registers`, `aging`, `isp`, `cash-flow`, `vat-regime`, `vies`,
`recargo`, `prorrata`, `cost-centers`) llaman a `/api/proxy/v1/accounting/...` con
datos reales del backend (✅ jul 2026). Otras rutas usan el patrón de proxy descrito
en ADR-0001 y descargan CSV/XML fiscales desde `AccountingExportController`.

### Modelo de datos
`JournalEntry` (1) → (N) `JournalEntryLine`, cada línea referencia una
`Account` (plan contable PGC) por `AccountId`/`AccountCode`. Cada asiento
lleva `SourceType`/`SourceId` para idempotencia y trazabilidad (p. ej.
`SourceType="Invoice"`, `SourceId=<InvoiceId>`). `FixedAsset` genera
`DepreciationSchedule` mensuales que a su vez producen `JournalEntry` de
dotación. `Budget` (1) → (N) `BudgetLine`, cada línea puede enlazar a
`CostCenter`. `Provision` puede tener un asiento de dotación y otro de
liberación asociados. Los modelos AEAT (`Modelo347`, `Modelo111And190`,
`Modelo200`, `Modelo202`) son entidades propias con sus registros de detalle.

### Flujo end-to-end representativo
Facturación → contabilización automática (asiento generado por evento de
dominio, sin intervención manual):
1. Billing publica `InvoiceApprovedEvent` (MediatR notification) al aprobar
   una factura — ver `BillingHandlers.cs`.
2. `InvoiceApprovedEventHandler`
   (`backend/Modules/Accounting/Application/Handlers/InvoiceApprovedEventHandler.cs`)
   la captura. Primero comprueba idempotencia (`JournalEntries.AnyAsync` con
   `SourceType="Invoice"` y `SourceId=InvoiceId`) para no duplicar el asiento
   si el evento se reprocesa.
3. Construye un `JournalEntry` con líneas: Debe 430 (Clientes) por el total,
   Haber 700 (Ventas) por la base, Haber 477 (IVA repercutido) si aplica,
   Debe 4751 (retenciones IRPF) si aplica, y Haber 4770 (recargo de
   equivalencia) si aplica — con fallback a acumular en 477 si la cuenta 4770
   no existe en el plan contable de la empresa.
4. Antes de guardar, valida que `Σ Debe == Σ Haber` (tolerancia 0,01 €) y
   lanza `InvalidOperationException("Asiento descuadrado...")` si no cuadra.
5. Al cobrar la factura, `MarkPaidHandler` publica `PaymentReceivedEvent`, que
   `PaymentReceivedEventHandler` convierte en un segundo asiento: Debe
   572/570 (Banco/Caja según método de pago) y Haber 430 (Clientes), también
   con control de idempotencia (`SourceType="Payment"`).
6. El usuario ve el asiento inmediatamente en `GET /api/accounting/journal`
   desde `frontend/src/app/accounting/page.tsx`, y puede exportarlo en el
   Libro Diario CSV vía `AccountingExportController`.
7. Al cierre trimestral, el usuario genera el Modelo 303 (JSON o XML oficial)
   desde `frontend/src/app/accounting/aeat` leyendo agregados reales de
   `JournalEntryLines`/`Invoices`/`Expenses`.

## Relación con otros módulos
- **Billing (ADR-0005):** origen de `InvoiceApprovedEvent` y
  `PaymentReceivedEvent`; Accounting es el consumidor que materializa los
  asientos. `AccountingExportController` también lee `IBillingDbContext`
  directamente para libros IVA y modelos AEAT.
- **Expenses (ADR-0007):** `ExpenseApprovedEventHandler`
  (`Application/Handlers/ExpenseApprovedEventHandler.cs`) genera asientos de
  gasto de forma análoga a `InvoiceApprovedEventHandler`.
- **Treasury (ADR-0012):** `BankReconciliationService` de Treasury lee
  `JournalEntryLines` de Accounting (cuenta 572) para conciliar movimientos
  bancarios contra apuntes contables — dependencia cruzada de solo lectura en
  sentido Treasury → Accounting.
- **Fiscal/SII/VeriFactu (ADR-0013):** los modelos AEAT (303, 390, 347, etc.)
  generados aquí son insumo para la presentación telemática que documenta
  ADR-0013; la validación VIES real está disponible en dos rutas equivalentes:
  `TaxController` (`api/tax/vies/validate`) y `ViesController` (con
  persistencia en `IntraEuOperations`, ADR-0018 #5).
- **CRM/Payroll:** `AccountingExportController` también agrega datos de
  `ICrmDbContext` e `IPayrollDbContext` para Modelo 347 y Modelo 190/111
  respectivamente (retenciones a trabajadores vía nóminas).

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

**Controllers delgados:** los 20 controllers del módulo usan `IMediator`
(ADR-0018 #3c ✅, #4, #5). Los cuatro controllers fiscales que eran mock
(`AeatModels`, `IvaManagement`, `InversionSujetoActivo`, `Aging`) ahora
persisten y calculan desde datos reales; `FinancialStatementsController`
delega en handlers con cálculo real parcial (#26).

**Pendiente (sin cerrar en backlog):** `IAccountingDbContext` expone 29 DbSets
(ISP — `GetFiscalPeriodsHandler` solo usa `FiscalPeriods`).
`VatController.DeclareModelo330` sigue sin implementar. OCP: tasas IVA en
`Dictionary`/`switch` (`CalculateVatCommand`).

**Corregido (backlog #4):** `AccountingExportController` (~350 líneas, 16 rutas)
delega todas las exportaciones en `IMediator` + exporters en Infrastructure;
ya no inyecta DbContexts cross-módulo ni contiene transformaciones inline.

**Corregido:** `VatController.cs` duplicaba tasas/cálculo de IVA que ya
existían, correctamente, en `CalculateVatCommand.cs` — el mismo patrón que ya
se había corregido para VIES. Ahora `VatController.CalculateVat` despacha
`CalculateVatCommand` vía `IMediator` (controller delgado, cumple CQRS) y
`GetVatRates` lee de `SpanishVatRates` (`Application/Features/Vat/VatRates.cs`),
la misma fuente de datos que usa el handler — ya no hay dos tablas de tasas
independientes. De paso, `CalculateVatCommand` dejó de aceptar `CompanyId`
como campo del body (el cliente podía enviar cualquier tenant) y ahora lo
resuelve del `ITenantContext` del handler, igual que el resto de comandos de
Accounting. `VatController.DeclareModelo330` sigue pendiente.

**Corregido:** `RecargoController` inyectaba `IBillingDbContext` directamente
con toda la lógica inline (queries a facturas con `SurchargeRate > 0`, agrupación
Modelo 303). Ahora despacha `GetRecargosQuery`, `GetRecargoByIdQuery`,
`CreateRecargoCommand` y `GenerateRecargoModelo303Command` vía `IMediator`
(`Application/Features/Recargo/RecargoHandlers.cs`). `CreateRecargoCommand` persiste
en `RecargoDEquivalencias` con validación NIF y tasas oficiales (0,5/1,4/5,2%).
El frontend
(`frontend/src/app/accounting/recargo/page.tsx`) dejó de llamar a
`/api/v1/accounting/recargo` (ruta inexistente en Next.js) y usa
`/api/proxy/v1/accounting/recargo` con el contrato real (`year`/`q` en GET,
`supplierVat`/`baseAmount`/`rechargeRate` en POST).

**Corregido:** `ViesController` tenía un diccionario estático de 4 NIFs de
prueba mientras `TaxController` ya usaba `IViesService` (SOAP VIES real). Ahora
`ViesController` despacha `ValidateViesCommand` vía `IMediator`, que invoca el
mismo `IViesService` y persiste la consulta en `IntraEuOperations`; el texto
`Advice` se centraliza en `Erp.Application/Common/ViesResponseMapper.cs` (también
usado por `TaxController`). El frontend
(`frontend/src/app/accounting/vies/page.tsx`) usa
`/api/proxy/v1/accounting/vies/validate` con contrato `{countryCode, vatNumber}`.

## Buenas prácticas aplicables
- Todo asiento generado automáticamente debe validar `Σ Debe == Σ Haber`
  antes de `SaveChangesAsync`, siguiendo el patrón de
  `InvoiceApprovedEventHandler`.
- Los manejadores de eventos deben comprobar idempotencia contra
  `SourceType`/`SourceId` antes de crear un asiento nuevo, para tolerar
  reprocesamiento de eventos.
- Las cuentas contables se resuelven siempre por `CompanyId` + `Code`
  (multi-tenant); nunca hardcodear un `AccountId`.
- Antes de añadir endpoints de IVA/modelos AEAT nuevos, verificar si ya existe
  lógica en `AccountingExportController`, `ReportsController` o
  `Erp.Api/Controllers/TaxController.cs` para no duplicar. No reintroducir
  controllers mock — implementar en handlers MediatR con datos reales.
- Respetar el prefijo `FiscalExportHeaders.MarkAsNonOfficial` en cualquier
  exportación fiscal nueva, para no inducir a pensar que sustituye la
  presentación oficial ante la AEAT.

## Consecuencias
- La madurez del módulo es heterogénea: cierre, presupuestos, activos fijos,
  provisiones, exportación fiscal y modelos 303/347 están sobre datos reales;
  quedan huecos puntuales (`RecargoController.Create`, `DeclareModelo330`,
  ISP/aging sin controller dedicado tras eliminar los stubs).
- **Corregido (ADR-0018 #14):** las entidades núcleo (`Account`,
  `JournalEntry`, `FiscalPeriod`, etc.) viven en
  `Modules/Accounting/Domain/Entities/`, no en `Erp.Domain`.
- **Corregido:** `ProrrataController.CalculateProrrata` ya no calcula inline
  con un `CalculateProrrataRequest` propio — ahora despacha
  `CalculateProrrataCommand` vía `IMediator`, que persiste un
  `ProrrataCalculation` real. Al revisar el contrato real (`frontend/src/app/accounting/prorrata/page.tsx`,
  que envía `{fiscalYear, inlandRevenue, exemptRevenue, type}` y lee
  `{inlandRevenue, exemptRevenue, prorrataPercentage}` de la respuesta) se
  detectó que el controller anterior usaba nombres de campo distintos
  (`DeductibleOperations`/`NonDeductibleOperations`/`TotalVatSupported`) que
  **no coincidían con lo que el frontend enviaba ni leía** — la página
  crasheaba en runtime al llamar `prorrata.prorrataPercentage.toFixed(2)`
  sobre `undefined`. El nuevo `CalculateProrrataCommand` usa exactamente los
  nombres de campo del frontend (es una calculadora manual: el usuario
  introduce los ingresos, no se derivan de `VatTransactions`) y corrige ese
  bug real de contrato, además de la duplicación de lógica.
- **Corregido (ADR-0018 #18):** `frontend/src/app/accounting/iva-registers/page.tsx`
  carga resumen real desde facturas/gastos, descarga CSV vía export y enlaza a
  `/sii` para envío.
- **Corregido (ADR-0018 #19b):** lectura cross-módulo de exportadores solo en
  `Accounting.Infrastructure`; `Accounting.Api` sin refs a otros módulos.

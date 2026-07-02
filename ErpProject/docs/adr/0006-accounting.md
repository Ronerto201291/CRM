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
Las entidades núcleo de contabilidad (`Account`, `JournalEntry`,
`JournalEntryLine`, `FiscalPeriod`) están definidas en el proyecto core
`Erp.Domain/Entities/Accounting/` en lugar de en el propio módulo, y se
consumen a través de `IAccountingDbContext`. El frontend vive bajo
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
- `AccountingController` (`api/accounting`) — diario (`journal`), balance de
  sumas y saldos, IVA soportado/repercutido, liquidación IVA, PyG, mayor por
  cuenta, y cierre contable (`GET/POST api/accounting/cierre`) vía
  `CloseFiscalYearCommand`.
- `AccountingExportController` (`api/accounting/export`, 1362 líneas) — el
  controlador más grande del módulo. Genera de forma real, leyendo datos de
  `IAccountingDbContext`, `IExpensesDbContext`, `IApplicationDbContext`,
  `IBillingDbContext`, `ICrmDbContext` e `IPayrollDbContext`: Libro Diario CSV,
  Modelo 303 (CSV y JSON estructurado en `api/accounting/modelo-303`, y XML
  oficial en `modelo303-xml`), Modelo 111 (JSON, retenciones IRPF), libros
  registro de facturas emitidas/recibidas (RIVA arts. 63-64), Modelo 347
  (CSV, TXT pipe-delimited, umbral 3.005,06 €, agrupado por NIF — art. 29 RD
  1065/2007), Modelo 190, Modelo 130, Modelo 200/202 IS (esqueletos XML),
  Modelo 390 (XML oficial AEAT, resumen anual de los 4 trimestres) y Modelo
  349 (operaciones intracomunitarias). Todas las respuestas de fichero pasan
  por `FiscalExportHeaders.MarkAsNonOfficial` (`Erp.Infrastructure.Fiscal`)
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

**IVA, modelos AEAT y fiscalidad especial (mayoritariamente stubs/mock):**
`AeatModelsController`, `ViesController`, `RecargoController` e
`IvaManagementController`/`InversionSujetoActivoController` (rutas bajo
`api/v1/accounting/{aeat,vies,recargo,iva,isp}`) exponen endpoints de
cálculo/consulta, pero la mayoría **devuelven datos simulados hardcodeados**
(`Guid.NewGuid()`, importes fijos, un diccionario VIES estático con 4 NIFs de
prueba) en lugar de persistir o leer del DbContext — son claramente
placeholders pendientes de conectar a datos reales. La excepción parcial es
`RecargoController`, cuyo `GetAll`/`GetById`/`modelo303` sí consultan
`IBillingDbContext.Invoices` reales filtrando por `SurchargeRate > 0`.
`VatController.CalculateVat` y `ProrrataController.CalculateProrrata` dejaron
de ser mock (ver Evaluación de calidad arquitectónica más abajo); `VatController.declare/modelo330`
sigue siendo un stub. El endpoint real y fiable de
validación VIES está en `Erp.Api/Controllers/TaxController.cs`
(`api/tax/vies/validate`, ver ADR-0013), no en `ViesController` del módulo
Accounting. `FinancialStatementsController` (`cash-flow`, `equity`) y
`AgingController` (`receivables`, `payables`) también devuelven datos fijos
de ejemplo.

### Frontend
`frontend/src/app/accounting/` contiene subrutas para cada área: `aeat`,
`aeat-models`, `aging`, `budgets`, `cash-flow`, `cierre`, `cost-centers`,
`depreciation`, `isp`, `iva-registers`, `prorrata`, `provisions`, `recargo`,
`reports`, `vat-regime`, `vies`, además de `page.tsx` (diario/balance/IVA/
liquidación con pestañas). Las páginas son client components (`'use client'`)
que llaman a `fetch('/api/proxy/accounting/...')` — el patrón de proxy Next.js
descrito en ADR-0001 — y descargan los CSV/XML fiscales generados por
`AccountingExportController` mediante blobs.

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
  ADR-0013; la validación VIES "real" vive en `Erp.Api/Controllers/TaxController.cs`,
  no en el `ViesController` (stub) de este módulo.
- **CRM/Payroll:** `AccountingExportController` también agrega datos de
  `ICrmDbContext` e `IPayrollDbContext` para Modelo 347 y Modelo 190/111
  respectivamente (retenciones a trabajadores vía nóminas).

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

Este es el módulo con más incumplimientos del checklist: `AccountingExportController.cs`
(1362 líneas, 19 endpoints, inyecta contexts de 6 módulos) es una violación
clara de SRP; `IAccountingDbContext` expone 29 DbSets (ISP, ver
`GetFiscalPeriodsHandler` que solo usa uno); y 9 de sus 16 controllers todavía
no usan `IMediator` — tienen lógica de negocio inline (los ya documentados
como mock: AeatModels, Vies, IvaManagement, InversionSujetoActivo, Prorrata,
Recargo, FinancialStatements, Aging, más `AccountingExportController`).

**Corregido:** `VatController.cs` duplicaba tasas/cálculo de IVA que ya
existían, correctamente, en `CalculateVatCommand.cs` — el mismo patrón que ya
se había corregido para VIES. Ahora `VatController.CalculateVat` despacha
`CalculateVatCommand` vía `IMediator` (controller delgado, cumple CQRS) y
`GetVatRates` lee de `SpanishVatRates` (`Application/Features/Vat/VatRates.cs`),
la misma fuente de datos que usa el handler — ya no hay dos tablas de tasas
independientes. De paso, `CalculateVatCommand` dejó de aceptar `CompanyId`
como campo del body (el cliente podía enviar cualquier tenant) y ahora lo
resuelve del `ITenantContext` del handler, igual que el resto de comandos de
Accounting. `VatController.DeclareModelo330` sigue siendo un stub sin tocar.

## Buenas prácticas aplicables
- Todo asiento generado automáticamente debe validar `Σ Debe == Σ Haber`
  antes de `SaveChangesAsync`, siguiendo el patrón de
  `InvoiceApprovedEventHandler`.
- Los manejadores de eventos deben comprobar idempotencia contra
  `SourceType`/`SourceId` antes de crear un asiento nuevo, para tolerar
  reprocesamiento de eventos.
- Las cuentas contables se resuelven siempre por `CompanyId` + `Code`
  (multi-tenant); nunca hardcodear un `AccountId`.
- Antes de extender los controladores "stub" (`AeatModelsController`,
  `ViesController`, `IvaManagementController`,
  `InversionSujetoActivoController`, `FinancialStatementsController`,
  `AgingController`), verificar si ya existe lógica real equivalente en
  `AccountingExportController` o en `Erp.Api/Controllers/TaxController.cs`
  para no duplicar. `VatController` y `ProrrataController` ya no están en
  esta lista (ver Evaluación de calidad arquitectónica).
- Respetar el prefijo `FiscalExportHeaders.MarkAsNonOfficial` en cualquier
  exportación fiscal nueva, para no inducir a pensar que sustituye la
  presentación oficial ante la AEAT.

## Consecuencias
- Existe una inconsistencia real de madurez dentro del módulo: los
  controladores de cierre, presupuestos, activos fijos, provisiones y
  exportación fiscal están completamente implementados sobre datos reales,
  mientras que buena parte de los controladores de IVA especial (recargo de
  equivalencia salvo lectura, ISP, prorrata, VIES local, modelos AEAT vía
  `AeatModelsController`) son stubs que devuelven datos de ejemplo — cualquier
  trabajo futuro debe verificar primero si un endpoint es real o mock antes de
  asumir su comportamiento.
- Las entidades núcleo (`Account`, `JournalEntry`) viven en `Erp.Domain`
  (core) y no en `Modules/Accounting/Domain`, lo que rompe ligeramente el
  aislamiento modular descrito en ADR-0001; cualquier cambio de esquema en
  esas entidades afecta directamente a Billing, Expenses y Treasury a través
  de `IAccountingDbContext`.
- El doble stub de validación VIES (uno real en `TaxController`, otro mock en
  `ViesController` de Accounting) es una fuente potencial de confusión si se
  usa el endpoint equivocado desde el frontend.
- **Corregido:** `Application/Features/Vat/ValidateViesCommand.cs` y
  `CalculateProrrataCommand.cs` eran código MediatR huérfano (ningún
  controller los invocaba — `ViesController` y `ProrrataController` tenían su
  propia lógica duplicada e independiente) que además **no compilaba**:
  escribían en `IntraEuOperation`/`ProrrataCalculation` usando propiedades
  que no existen en esas entidades, rompiendo el build de todo el backend
  desde el primer commit del repo. `ValidateViesCommand` ahora invoca el
  `IViesService` real (el mismo que usa `TaxController`, SOAP contra la UE)
  en vez de un diccionario de NIFs de prueba hardcodeado; `ViesController`
  sigue sin tocar (mock, endpoint separado).
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

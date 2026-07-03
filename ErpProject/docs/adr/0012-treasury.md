# ADR-0012: Treasury

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El módulo Treasury gestiona la tesorería de la empresa: cuentas bancarias,
movimientos, conciliación bancaria automática contra los asientos contables,
efectos comerciales (letras), órdenes de pago, previsión de flujo de caja,
divisas/tipo de cambio, financiación alternativa (confirming, factoring,
líneas de crédito), avales/garantías/colateral y consolidación de grupos de
empresas (multi-sociedad). Sigue la estructura estándar descrita en ADR-0001
(`backend/Modules/Treasury/{Api,Application,Domain,Infrastructure}`). El
frontend vive en `frontend/src/app/treasury/`.

## Decisión
### Backend
`TreasuryDbContext`
(`backend/Modules/Treasury/Infrastructure/Data/TreasuryDbContext.cs`) expone
`BankAccounts`, `BankMovements`, `CashEffects`, `ReconciliationBatches`,
`CashFlowForecasts`, `PaymentOrders`, `Currencies`/`CurrencyExchanges`/
`ExchangeRateHistories`, `ConfirmingOperations`/`FactoringOperations`/
`FinancingAccounts`, `Guarantees`/`Collaterals`/`BankGuarantees`, y las
entidades de consolidación `ConsolidationGroups`/`SubsidiaryCompanies`/
`ConsolidationAdjustments`/`ConsolidatedFinancialStatements`/
`IntercompanyTransactions`. Migraciones: `InitialCreate` y
`Phase4TreasuryFinancingGroups`.

Cinco controladores en `Api/Controllers/`:
- **`TreasuryController`** (`api/treasury`) — el núcleo operativo: cuentas
  bancarias (`bank-accounts`), movimientos paginados (`page`/`pageSize`,
  `X-Total-Count`), importación de extracto CSV (`POST
  bank-accounts/{id}/import`, formato `Fecha,Importe,Concepto,Referencia`),
  conciliación automática (`POST bank-accounts/{id}/reconcile`, vía
  `ReconcileBankAccountCommand`), efectos comerciales paginados (`effects`),
  órdenes de pago paginadas (`payment-orders`) y previsión de caja
  (`forecasts`, filtrable por año/mes). **Corregido (backlog #9):** controller
  delgado vía `IMediator`; handlers en
  `Application/Features/Treasury/Handlers/TreasureHandlers.cs`.
- **`CurrenciesController`** (`api/v1/treasury/currencies`) — CRUD de
  divisas por empresa, tipos de cambio (`rates`) y conversión (`POST
  exchange`). ✅ Migrado a CQRS (backlog #9): despacha `GetCurrenciesQuery`,
  `CreateCurrencyCommand`, `UpdateCurrencyRateCommand`, `DeleteCurrencyCommand`,
  `GetCurrencyRatesQuery` y `ExchangeCurrencyCommand` vía `IMediator`
  (`Application/Features/Currencies/CurrencyHandlers.cs`). `IExchangeRateService`
  movido a `Application/Interfaces/` (antes definido en Infrastructure).
- **`FinancingController`** (`api/v1/treasury/financing`) — confirming
  (anticipo a proveedores), factoring (anticipo de cobro a clientes) y
  líneas de crédito (`credit-lines`). **Corregido (backlog #9):** controller
  delgado vía `IMediator`; handlers en
  `Application/Features/Financing/FinancingHandlers.cs`.
- **`GuaranteesController`** (`api/v1/treasury/guarantees`) — avales
  genéricos (con reclamación `claim` y liberación `release`), colateral
  (`collateral`, con ratio LTV) y avales bancarios específicos
  (`bank-guarantees`). **Corregido (backlog #9):** controller delgado vía
  `IMediator`; handlers en `Application/Features/Guarantees/GuaranteeHandlers.cs`.
- **`ConsolidationController`** (`api/v1/treasury/consolidation`) — grupos de
  consolidación, filiales (`subsidiaries` con `OwnershipPercentage`/
  `VotingPercentage`/`ConsolidationMethod`), estados financieros consolidados,
  transacciones intercompañía y su eliminación (`POST
  {groupId}/eliminate-intercompany`, marca `IsEliminated=true` en bloque vía
  `ExecuteUpdateAsync`). **Corregido (backlog #9):** controller delgado vía
  `IMediator`; handlers en `Application/Features/Consolidation/ConsolidationHandlers.cs`.
  El endpoint `POST {groupId}/consolidate` valida que
  el grupo exista pero solo devuelve un estado `"Consolidated"` con timestamp,
  sin generar aún estados financieros reales — apunta a lógica de
  consolidación pendiente de completar.

Todos los controladores son `[Authorize]` y resuelven `CompanyId` vía
`ITenantContext.TenantId`, filtrando explícitamente cada consulta EF Core por
tenant (patrón multi-tenant de ADR-0002).

`BankReconciliationService`
(`Infrastructure/Services/BankReconciliationService.cs`) implementa el
algoritmo de conciliación en tres fases sobre movimientos no conciliados
(`Origin != "System"`): Fase 1 — importe exacto + fecha exacta; Fase 2 —
importe exacto + referencia de factura detectada por regex
(`[A-Z]*-?\d{4,}`) en la descripción del movimiento; Fase 3 — importe exacto
+ fecha ±3 días (match "medio", requeriría confirmación). Cruza
`BankMovements` de Treasury contra `JournalEntryLines` de **Accounting**
(cuentas que empiezan por `572`, filtradas por `IsPosted=true` y año actual/
anterior) — es decir, la conciliación depende directamente del DbContext de
Accounting (`IAccountingDbContext`), no solo del propio módulo.

### Frontend
`frontend/src/app/treasury/page.tsx` — panel principal con pestañas
`accounts | movements | effects | orders | forecast`, llamando a
`fetch('/api/proxy/treasury/...')` (patrón de proxy de ADR-0001). Subrutas
dedicadas: `frontend/src/app/treasury/{consolidation,currencies,financing,
guarantees}/page.tsx`, cada una consumiendo su controlador homónimo.

### Modelo de datos
`BankAccount` (1) → (N) `BankMovement` y (N) `CashEffect`. Cada
`BankMovement` puede quedar `IsReconciled=true` con
`MatchedJournalEntryLineId` apuntando a una línea de asiento de Accounting, y
`ReconciliationBatchId` agrupando el lote (`ReconciliationBatch`, con
`ItemsCount`/`TotalAmount`/`Type="Auto"`). `PaymentOrder` referencia
opcionalmente `BankAccountId` y un `SourceType`/`SourceId` genérico (factura o
gasto origen) y `GeneratedBankMovementId` una vez ejecutada. `Financing`
(`ConfirmingOperation`, `FactoringOperation`, `FinancingAccount`) y
`Guarantees` (`Guarantee`, `Collateral`, `BankGuarantee`) son entidades
independientes sin relación de clave foránea fuerte con el resto del módulo,
solo `CompanyId`. `ConsolidationGroup` (1) → (N) `SubsidiaryCompany`, y
agrega `ConsolidatedFinancialStatement` e `IntercompanyTransaction` por
grupo.

### Flujo end-to-end representativo
Importación de extracto bancario → conciliación automática contra
contabilidad:
1. Desde `frontend/src/app/treasury/page.tsx`, el usuario sube un CSV de
   extracto bancario para una cuenta (`POST
   /api/proxy/treasury/bank-accounts/{id}/import`).
2. `TreasuryController.ImportStatement` parsea línea a línea (formato
   `Fecha,Importe,Concepto,Referencia`) y crea `BankMovement` con
   `Origin="BankImport"`, `IsReconciled=false`.
3. El usuario dispara `POST
   /api/proxy/treasury/bank-accounts/{id}/reconcile`;
   `TreasuryController.Reconcile` resuelve `BankReconciliationService` desde
   DI y llama a `ReconcileAsync(bankAccountId)`.
4. El servicio lee los movimientos no conciliados de esa cuenta y las líneas
   de asiento de cuenta 572 (o el `AccountingAccountCode` configurado en la
   `BankAccount`) desde `IAccountingDbContext.JournalEntryLines` del último
   año/año anterior, y aplica las tres fases de matching descritas arriba.
5. Los movimientos emparejados se marcan `IsReconciled=true` y se agrupan en
   un `ReconciliationBatch`; el resultado (`MatchedCount`, `MatchedAmount`)
   se devuelve al frontend.

## Relación con otros módulos
- **Accounting (ADR-0006):** relación de lectura directa y fuerte —
  `BankReconciliationService` inyecta `IAccountingDbContext` para conciliar
  contra `JournalEntryLines` de cuenta 572. Es la única dependencia
  cross-módulo real detectada en Treasury a nivel de código (no vía eventos,
  sino acceso directo al DbContext de otro módulo).
- **Billing/Sales (ADR-0005/ADR-0011) — cobro de facturas:** al marcar una
  factura como pagada, `MarkPaidHandler` (Billing) publica
  `PaymentReceivedEvent`, que es consumido por
  `PaymentReceivedEventHandler` en Accounting (genera el asiento 572/430) y
  por `PaymentReceivedActivityHandler` en CRM
  (`Modules/Crm/Application/EventHandlers/PaymentReceivedActivityHandler.cs`,
  registra actividad en el cliente — ver ADR-0004). **No se encontró ningún
  handler en Treasury que consuma `PaymentReceivedEvent` directamente**: el
  módulo no se entera del cobro vía evento de dominio, sino que su
  conciliación bancaria opera de forma independiente y retrospectiva
  (importar extracto real del banco + emparejar contra los asientos que
  Accounting ya generó). Es decir, la cadena real es Billing → evento →
  Accounting (asiento) → Treasury (concilia el movimiento bancario contra ese
  asiento, en un paso posterior y desacoplado), no Billing → Treasury
  directamente.
- **Expenses/Purchasing:** `PaymentOrder` tiene un `SourceType`/`SourceId`
  genérico pensado para enlazar con facturas de proveedor o gastos, pero no
  se detectó lógica automática que cree órdenes de pago desde esos módulos —
  la creación es manual vía `POST payment-orders`.

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

Los cinco controllers de Treasury delegan en `IMediator` (backlog #9
completado). `ITreasuryDbContext` expone 20 DbSets (ISP). La dependencia de
`BankReconciliationService` / `ReconcileBankAccountHandler` sobre
`IAccountingDbContext` (ver Relación con otros módulos) es acceso directo a
otro módulo, no vía evento — acoplamiento real, no solo de lectura trivial.

## Buenas prácticas aplicables
- Cualquier nuevo cruce de datos de Treasury contra Accounting debe seguir el
  patrón de `BankReconciliationService`: inyectar `IAccountingDbContext`
  explícitamente en el servicio de Treasury, en vez de intentar acceder a
  entidades de Accounting desde otro camino.
- Todas las consultas deben filtrar por `CompanyId == _tenantContext.TenantId`
  antes de cualquier otra condición, replicando el patrón usado en los cinco
  controladores.
- Si se añade un nuevo tipo de movimiento u operación financiera, mantener el
  campo `Status` como cadena de estados simples (`"Draft"`, `"Active"`,
  `"Paid"`, `"Cancelled"`, etc.) consistente con el resto del módulo, en
  lugar de introducir un enum aislado.
- Antes de asumir que Treasury reacciona automáticamente a un cobro o pago de
  otro módulo, verificar si existe de verdad un `INotificationHandler` en
  `Modules/Treasury/Application/` — a día de hoy no lo hay para
  `PaymentReceivedEvent`.

## Consecuencias
- La falta de un handler de `PaymentReceivedEvent` en Treasury significa que
  el saldo de `BankAccount.CurrentBalance` y las previsiones de
  `CashFlowForecast` no se actualizan automáticamente al cobrar una factura;
  dependen de la importación manual/periódica del extracto bancario real y
  de la conciliación posterior. Cualquier feature que necesite tesorería en
  tiempo real debe tenerlo en cuenta.
- `ConsolidationController.ConsolidateGroup` es, en la práctica, un stub:
  valida la existencia del grupo y responde `"Consolidated"`, pero no genera
  `ConsolidatedFinancialStatement` reales ni aplica `ConsolidationAdjustments`
  — confirmado que esa tabla **nunca se escribe en ningún punto del código**,
  así que `GetFinancialStatements` (que lee de ahí) siempre devuelve vacío.
  La consolidación de grupos multi-sociedad no está implementada, solo
  aparenta estarlo (ver catálogo de mock en ADR-0018, ítem 15 del backlog).
- `ExchangeRateRefreshJob` (actualización diaria de tipos de cambio desde el
  BCE) nunca hace nada en la práctica: corre como `BackgroundService` fuera
  de un request HTTP, y `TenantContext.TenantId` solo lo puebla
  `TenantResolverMiddleware` por petición — fuera de ese contexto siempre es
  `null`, así que el job hace no-op silencioso todos los días, sin log ni
  error. `EcbExchangeRateProvider` (el cliente HTTP real contra el BCE) está
  bien implementado pero es inalcanzable en producción tal como está cableado
  (ver ADR-0018, ítem 16 del backlog).
- La conciliación bancaria automática es heurística (coincidencia de importe
  + fecha/regex de referencia) y no criptográficamente determinista; en
  cuentas con muchos movimientos del mismo importe y fecha puede producir
  falsos positivos si no se revisa el lote generado.
- Financiación (confirming/factoring/líneas de crédito) y garantías/avales no
  generan asientos contables automáticos hacia Accounting en el código
  revisado — quedan registrados solo en Treasury, a diferencia del flujo de
  facturas/pagos que sí dispara asientos vía eventos.

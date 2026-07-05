# Plan de cobertura de tests — ERP SaaS España



Auditoría jul 2026. Objetivo: CI ejecuta tests en backend + frontend con al menos

un smoke test por módulo/área; ampliación incremental hacia cobertura exhaustiva.



## Inventario actual (jul 2026, fase 18 — backlog opcional ADR-0018)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **578 unit tests** (+ `Phase18HandlerTests`: fixed assets, deferred entries, contacts, recargo) |

| Páginas frontend | ~77 `page.tsx` | **129 tests Vitest** (+ `NewDeliveryNotePage`, `ClientEditorClient`; smoke RSC con `await Page()`) |

| Architecture tests | 4 reglas | **5** (`Erp.ArchitectureTests`) |

| Integration (Testcontainers) | Postgres + Redis | **152 tests** (+ `PostgresRlsBootstrapTests`) |

| E2E Playwright | 19 | 12 smoke + 6 business flows + **verify TOTP completo** (`E2E_2FA_TOTP_SECRET` + otplib) |

**Total CI:** ~**763 tests** (578 unit + 152 integration + 129 frontend + 19 E2E). Gates: merged **52%**, unit **27%** (objetivo 30% pendiente medición ~26–28%), Accounting.Application **22%**, frontend líneas **43%** (medido ~45%).

**Lint frontend:** 0 errores; **0 warnings `react-hooks/*`** (26 warnings restantes: `no-unused-vars`, `@next/next/no-img-element`).

**RLS (#34):** 19 tablas — 10 core + 9 módulo (`billing.Invoices/Quotes`, `crm.Clients/Suppliers/Leads/Contacts`, `expenses.ExpenseDocuments`, `sales.SalesOrders`, `purchasing.PurchaseOrders`).

## Inventario anterior (jul 2026, fase 17 — ROI tests)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~477 unit tests** (+ `AccountingServiceTests`, `StripeWebhookHandlerTests` firmado) |

| Páginas frontend | ~77 `page.tsx` | **123 tests Vitest** |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **141 tests** (+ `Phase17AccountingHttpTests`, `StripeWebhookHttpTests`) |

| E2E Playwright | 18 | 12 smoke + **6 business flows** (`business-flows.spec.ts`) |

**Total CI:** ~**759 tests** (477 unit + 141 integration + 123 frontend + 18 E2E). Gate Accounting.Application **20%** (medido integration ~22.7%).

## Inventario anterior (jul 2026, fase 16 — plan cerrado)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~472 unit tests** (Phase16: GetLeadById, GetLeads status, payment orders, cash effects, convert lead guard) |

| Páginas frontend | ~77 `page.tsx` | **123 tests Vitest** (+ `ProvisionsClient`, `AgingClient`, `DeliveriesClient`) |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **137 tests**: fase 15 + **Phase16IntegrationHttpTests** (POST leads 201, convert lead, execute payment order, AnulVerifactu HTTP, provisions, aging, deliveries) |

| E2E Playwright | 12 smoke | + 2FA condicional (`E2E_2FA_*`) |

**Total CI:** ~**748 tests** (613 backend + 123 frontend + 12 E2E). Coverlet umbral **50% línea** en `Erp.Tests` e `Erp.IntegrationTests` (verificado Release local).

**Medición XPlat Code Coverage (referencia amplia backend, jul 2026 fase 16):** unit ~**28.2%** línea (estable vs fase 15), integración ~**51.2%** línea (+2.4 pp, **≥50% objetivo**); umbral csproj **50%** sin subir.

**Fix producción fase 16:**

- **POST `/api/leads` → 500:** migraciones CRM manuales (`AddLeadProspectFields`, `AddCrmNotes`, `AddScheduledAlerts`) sin atributo `[Migration]` — EF no las aplicaba; faltaban columnas `TaxId`/`Address` en `crm.Leads`.
- **Provisiones / aging:** mismas migraciones accounting sin `[Migration]` (`Phase2ContabilityAndAnalytics`, `Phase3VatAndFiscality`, `AddAmortizationsAndDeferredEntries`) — tabla `accounting.Provisions` no existía.
- **AnulVerifactu integración:** `NoOpVerifactuSubmissionGateway` en IntegrationTests (sin Hangfire).
- **ExecutePaymentOrder HTTP:** `POST /api/treasury/payment-orders/{id}/execute` en `TreasuryController`.

**Plan fase 16:** **~100% cerrado**. Pendiente menor: unit XPlat 30%+ (28.2%), E2E verify TOTP completo (requiere `E2E_2FA_TOTP_SECRET`).

## Inventario anterior (jul 2026, fase 15)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~466 unit tests** (Phase15: AcceptInvite, GetInvoicePdf/GetQuotePdf, AnulVerifactu, ReconcileBankAccount) |

| Páginas frontend | ~77 `page.tsx` | **114 tests Vitest** (+ `EmpresasClient`, `InvoiceDetailClient`, `FinancingClient`, `FacturaEClient`, `OrderDetailClient`, `ConsolidationClient`, `WarehousesClient`, `CreditNotesClient`, `SalesInvoicesClient`, `GuaranteesClient`) |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **130 tests**: fase 14 + **Phase15IntegrationHttpTests** (accept-invite, PDF invoice/quote, reconcile bank, verifactu submissions, contacts, warehouses, sales orders, guarantees, credit notes list) |

| E2E Playwright | 11 smoke | + facturae page |

**Total CI:** ~**725 tests** (600 backend + 114 frontend + 11 E2E). Coverlet umbral **50% línea** en `Erp.Tests` e `Erp.IntegrationTests` (verificado Release local).

**Medición XPlat Code Coverage (referencia amplia backend, jul 2026 fase 15):** unit ~**28.2%** línea (+0.7 pp vs fase 14), integración ~**48.8%** línea (+3.1 pp); umbral csproj **50%** sin subir — integración XPlat ya en rango 48–50% objetivo.

**Fix producción fase 15:** `/api/auth/accept-invite` añadido a rutas públicas en `TenantResolverMiddleware` (antes 401 «Tenant ID requerido»).

**ExecutePaymentOrder HTTP:** handler cubierto en unit (`Phase14HandlerTests`); **no existe** `POST .../payment-orders/{id}/execute` en `TreasuryController` — pendiente producto o documentado como solo MediatR interno.

**AnulVerifactu HTTP integración:** omitido en CI — `VerifactuSubmissionGateway` usa Hangfire (`BackgroundJob.Enqueue`) no inicializado en `IntegrationTests`; cubierto en unit (`Phase15AnulVerifactuHandlerTests`).

## Inventario anterior (jul 2026, fase 14)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~451 unit tests** (Phase14: login/invite, users/admin, billing público, treasury execute, fiscal calendar, verify API key, email confirmation) |

| Páginas frontend | ~77 `page.tsx` | **97 tests Vitest** (+ `UsersClient`, `ApiKeysClient`, `AuditLogsClient`, `QuotesClient`, `SalesOrdersClient`) |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **120 tests**: fase 13 + **Phase14IntegrationHttpTests** (users, fiscal calendar, public invoices, audit logs, facturae locked validate, API verify) |

| E2E Playwright | 10 smoke | + settings/usuarios |

**Total CI:** ~**682 tests** (575 backend + 97 frontend + 10 E2E). Coverlet umbral **50% línea** en `Erp.Tests` e `Erp.IntegrationTests` (verificado Release local).

**Medición XPlat Code Coverage (referencia amplia backend, jul 2026 fase 14):** unit ~**27.5%** línea (+1.3 pp vs fase 13), integración ~**45.7%** línea (+1.4 pp); umbral csproj **50%** sin subir — XPlat global monolito aún <50%.

## Inventario anterior (jul 2026, fase 13)

| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~425 unit tests** (Phase13: auth 2FA, permissions, refresh/email, FacturaE, subscriptions checkout/portal, anonymize contact/supplier, Stripe webhook guards) |

| Páginas frontend | ~77 `page.tsx` | **88 tests Vitest** (+ `ContactsClient`, `SuppliersClient`, `SubscriptionClient`, `AutomationClient`, `QuoteDetailClient`) |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **113 tests**: fase 12 + **Phase13IntegrationHttpTests** (subscription, contacts CRUD, permissions/my, company, tenant modules, facturae validate 404) |

| E2E Playwright | 9 smoke | sin cambio fase 13 |

**Total CI:** ~**639 tests** (542 backend + 88 frontend + 9 E2E). Coverlet umbral **50% línea** en `Erp.Tests` e `Erp.IntegrationTests` (verificado Release local).

**Medición XPlat Code Coverage (referencia amplia backend, jul 2026 fase 13):** unit ~**26.2%** línea (+1.2 pp vs fase 12), integración ~**44.3%** línea (+2.6 pp); umbral csproj **50%** sin subir — XPlat global monolito aún <50%.

## Inventario anterior (jul 2026, fase 12)



| Área | Cantidad | Tests existentes |

|---|---|---|

| Controllers API | ~54 | 1 HTTP smoke 401 por controller (`ControllerUnauthorizedTests`) |

| Handlers MediatR | ~200+ clases | **~304 unit tests** (Phase9/10 barrido handlers, Billing quotes send/convert, CRM alerts, Purchasing goods receipt, …) |

| Páginas frontend | ~77 `page.tsx` | **68 tests Vitest** (CurrenciesClient crear divisa, ExpensesClient edit PUT, FiscalClient, formularios create, hooks, smoke módulos) |

| Architecture tests | 4 reglas | `Erp.ArchitectureTests` |

| Integration (Testcontainers) | Postgres + Redis | **94 tests**: 401 smoke, health, migraciones, JWT auth 200, CRUD cliente/factura, **QuoteFullFlow Postgres** (send→accept→convert→lock→asiento), **UpdateQuotePostgres**, **SalesOrderFlowPostgres**, treasury v1, fiscal smoke |

| E2E Playwright | 9 smoke | `frontend/e2e/smoke.spec.ts` — login→dashboard, clientes, crear cliente, fiscal, treasury, expenses, **upload gasto token QR** (condicional stack), health |



**Total CI:** ~**479 tests** (402 backend + 68 frontend + 9 E2E). Coverlet umbral **50% línea** en `Erp.Tests` e `Erp.IntegrationTests` (verificado Release local).



**Medición XPlat Code Coverage (referencia amplia backend, jul 2026):** unit ~**20.3%** línea, integración ~**38.6%** línea sobre todo el monolito (`--collect:"XPlat Code Coverage"`). El umbral coverlet del csproj mide un scope distinto (assemblies incluidos por el collector) y pasa al **50%** configurado.



## Estrategia por capa



### Backend — unit (`Erp.Tests`)



- **Patrón:** EF InMemory + `FakeTenantContext` (+ fakes compartidos en `TestSupport/`).

- **Fase 8 añadido:** Billing quotes (`GetQuote`, Accept/Reject guards, Duplicate, GetQuotes), `MarkPaidHandler`, `VerifyHashChainHandler`; Payroll settlements (create/add line/finalize), Export TC2/RED orientativo; Sales `CreateDeliveryNote`, `GetSalesOrders`; fakes `FakeMediator`, `FakePayrollJournalEntryGenerator`.

- **Fase 9 añadido:** Quote handlers Postgres (`ExecuteUpdate`/`ExecuteDelete` para evitar `DbUpdateConcurrencyException`); integración `QuoteFlowPostgresTests`, `UpdateQuotePostgresTests`, `SalesOrderFlowPostgresTests`; unit Phase9/10 (+37): CRM delete/anonymize, Expenses upload/delete line, Inventory adjust stock, Purchasing update/delete PO, Alerts, UpdateClient, CreateGoodsReceipt, GetMayor, SendInvoiceEmail guard, NewQuoteVersion guard; fakes `FakeBillingServices`, `NoOpEmailService` en IntegrationTests.

- **Fase 10 añadido:** +48 unit (exports AEAT Modelo111/390/347/349/LibroIva/XML, alerts CRUD, expense queries, outbox handlers, CreateSupplierInvoice 3-way match, balance/PyG, fixed assets, VIES/prorrata, billing module invoice, inventory valuation, goods receipt inventory event); +3 integración (`ModuleFlowPostgresTests`: purchasing, payroll, expenses); +2 frontend (`InventoryClient`); E2E upload retries/timeouts CI.

- **Fase 11 añadido:** fix `ApproveExpenseHandler` AuditLog con `IHttpContextCurrentUserAccessor` (evita FK `Guid.Empty` en Postgres); +12 unit (CRM outbox restantes, serials, treasury forecast/SEPA); +4 integración HTTP (`Phase11IntegrationHttpTests`: approve→asiento, SEPA, forecast, automation CRUD); +3 frontend (`PayrollClient`, `AeatClient`); E2E upload fail-hard en CI; health wait 90×5s.

- **Fase 12 añadido:** fix `GetLiquidacionIVAHandler` fechas UTC (Postgres timestamptz); +26 unit (`Phase12HandlerTests`: CRM, accounting queries/export, treasury, inventory, expenses); +6 integración HTTP (`Phase12IntegrationHttpTests`: reportes, accounting, export, CRM, warehouses, payment orders); +5 frontend (`ReportsClient`, `TreasuryPage` forecast); E2E upload 3 reintentos con backoff.

- **Fase 13 añadido:** +35 unit (`TwoFactorHandlerTests`, `AuthEmailAndPermissionTests`, `FacturaEHandlerTests`, `Phase13HandlerTests` subscriptions, `StripeWebhookHandlerTests`, anonymize contact/supplier); +6 integración HTTP (`Phase13IntegrationHttpTests`: subscription, contacts, permissions, company, tenant modules, facturae); +10 frontend (`ContactsClient`, `SuppliersClient`, `SubscriptionClient`, `AutomationClient`, `QuoteDetailClient`).

- **Fase 14 añadido:** +26 unit (`Phase14HandlerTests` auth/users/admin/billing público/treasury/fiscal); +7 integración HTTP (`Phase14IntegrationHttpTests` users, fiscal calendar, public invoices, audit logs, facturae locked, API verify); +9 frontend (`UsersClient`, `ApiKeysClient`, `AuditLogsClient`, `QuotesClient`, `SalesOrdersClient`); E2E settings/users.

- **Fase 15 añadido:** +15 unit (`Phase15HandlerTests` AcceptInvite, GetInvoicePdf, GetQuotePdf, AnulVerifactu, ReconcileBankAccount); +10 integración HTTP (`Phase15IntegrationHttpTests` accept-invite, PDF, reconcile, verifactu submissions, barrido módulos); +10 frontend client components (+17 casos Vitest); E2E facturae page; fix middleware accept-invite público.

- **Fase 16 añadido (plan cerrado):** fix migraciones CRM/accounting sin `[Migration]` (POST leads 500, provisiones); +6 unit (`Phase16HandlerTests`); +7 integración HTTP (`Phase16IntegrationHttpTests` leads/convert/execute PO/AnulVerifactu/provisions/aging/deliveries); +9 frontend (`ProvisionsClient`, `AgingClient`, `DeliveriesClient`); E2E 2FA condicional; `POST payment-orders/{id}/execute`; `NoOpVerifactuSubmissionGateway` integración.



### Backend — integración (`Erp.IntegrationTests`)



- **`PostgresWebApplicationFactory`** + `TestAuthHelper` + `IntegrationTestDatabaseMigrator`.

- **Auth HTTP centralizada (ADR-0018 #42c):** `IntegrationTestAuth.RegisterEnterpriseAsync` — register real → upgrade plan Enterprise → habilitar todos los `PlanModules` incluidos como `TenantModule` (`IntegrationTestModuleHelper`). Usar en cualquier test que llame endpoints con `[RequiredModule]`. `RegisterFreeAsync` solo para multi-tenant en CRM/Billing, webhooks de cambio de plan o tests que esperan 403 (`ModuleAuthorizationEndToEndTests`).

- **Flujos autenticados:** register → JWT → CRUD cliente/factura; GET invoice; **POST v1 eliminate-intercompany** JWT + X-Api-Key.

- **Fase 8:** `PublicApiV1TreasuryHttpTests` — GET `/api/v1/treasury/currencies`, `/financing/confirming`, `/financing/credit-lines`, `/consolidation` con JWT + X-Api-Key.

- **Fiscal smoke JWT:** homologation status, SII validate, Verifactu XML.



### Backend — arquitectura (`Erp.ArchitectureTests`)



- Controllers sin DbContext, Domain sin EF, IMediator obligatorio, sin parseo inline.



### Frontend — Vitest + Testing Library



- **Hecho:** formularios create invoice/order/purchase/expense; **LeadsClient edit (PUT)**, **BillingClient mark paid**, **FiscalClient** smoke; hooks; schemas.

- **Pendiente:** hooks dedicados fiscal/treasury si se extraen; más páginas edit/update; payroll/accounting exports ampliados.



### E2E — Playwright (`frontend/`)

- Credenciales seed docker: `admin@devcorp.com` / `DevChangeMe2026!!` (override `E2E_ADMIN_*`).
- **`smoke.spec.ts`**: login, páginas módulo, upload QR, 2FA condicional.
- **`business-flows.spec.ts`** (fase 17): flujos negocio vía UI + proxy autenticado — cliente→factura borrador, gasto→asiento, pedido→albarán, presupuesto, lock→diario, proveedor. Helpers en `e2e/helpers.ts`.
- CI **push main + pull_request**: stack docker + wait health (:3000 + :8081/health) + smoke + business flows.



## Coverage gates en deploy (fase 17, jul 2026)

**Objetivo:** el pipeline CI **falla y bloquea deploy** (build Docker en `main`, job E2E) si la cobertura baja del umbral configurado. No es decorativo: `scripts/check-coverage.py` sale con código 1 y Vitest aplica `thresholds` en `vitest.config.ts`.

### Umbrales activos (`scripts/coverage-thresholds.json`)

| Gate | Umbral mínimo | Medido jul 2026 | Qué pasa si baja |
|------|---------------|-----------------|------------------|
| Backend merged (unit+integration XPlat) | **49%** línea | ~51% | Job `backend-build` falla → sin E2E ni Docker |
| Backend unit XPlat | **27%** línea | ~28.2% | Idem |
| Backend integration XPlat | **49%** línea | ~51.2% | Idem |
| `Erp.Modules.Billing.Application` | **55%** | ~59.7% | Idem (módulo crítico) |
| `Erp.Modules.Accounting.Application` | **20%** | ~22.7% | Idem (módulo crítico, subido fase 17) |
| `Erp.Infrastructure` (auth) | **50%** | ~55.1% | Idem |
| Frontend Vitest (scope clientes) | **39%** líneas | ~53% | Job `frontend-build` falla → sin E2E ni Docker |

**Nota:** el umbral coverlet **50%** en csproj mide un scope distinto (assemblies referenciados por cada proyecto de test) y **no** sustituye al gate XPlat. El gate real es `check-coverage.py` + Vitest thresholds.

### CI (`/.github/workflows/ci-cd.yml` en la raíz del monorepo)

Pipeline único en la raíz (`c:\CRM\.github\workflows\ci-cd.yml`); `ErpProject/.github/workflows/` solo documenta la redirección.

1. `backend-build`: unit + integration con `--collect:"XPlat Code Coverage"` → `ErpProject/scripts/check-coverage.py`
2. `frontend-build`: `npm run test:coverage` (Vitest v8 + umbrales)
3. `e2e-playwright`: `needs: [backend-build, frontend-build]`
4. `docker-build` + `deploy` (Hetzner): solo `push` a `main`, tras pasar E2E y coverage gates

### Comandos locales

```bash
# Backend gate completo (bash + Python 3)
bash ErpProject/scripts/run-backend-coverage-gate.sh

# Frontend gate
cd ErpProject/frontend && npm run test:coverage

# Medición manual XPlat
cd ErpProject/backend
dotnet test tests/Erp.Tests/Erp.Tests.csproj -c Release \
  --collect:"XPlat Code Coverage" -- \
  DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=cobertura
```

### Roadmap hacia cobertura «total»

| Objetivo | Actual | Gate actual | Meta trimestral |
|----------|--------|-------------|-----------------|
| Backend merged XPlat | ~51% | 49% | 55% |
| Backend unit XPlat | ~28.2% | 27% | 35% |
| Frontend líneas (clientes) | ~53% | 39% | 50% |
| Accounting.Application | ~22.7% | 20% | 35% |

Pendiente para «total»: E2E verify TOTP (`E2E_2FA_TOTP_SECRET`), más handlers sin unit test, páginas frontend sin Vitest.

## CI (`/.github/workflows/ci-cd.yml`)

Jobs: `backend-build` → `frontend-build` (paralelos) → `e2e-playwright` → `docker-build` (solo `main`) → `deploy` Hetzner (solo `main`).

```bash
# Backend gate (desde raíz monorepo)
bash ErpProject/scripts/run-backend-coverage-gate.sh

# Frontend gate
cd ErpProject/frontend && npm ci && npm run test:coverage && npm run build
```

**Gate bloqueante** en `ErpProject/scripts/check-coverage.py` (merged ≥49%, unit ≥27%, módulos críticos). Frontend: Vitest thresholds ≥39% líneas. Upload Codecov opcional con secret `CODECOV_TOKEN`.



## Backlog de ampliación (priorizado)

Plan fase 16 **cerrado (~100%)**. Fase 17: **coverage gates en deploy** (activos). Mantenimiento incremental:

1. Subir cobertura XPlat unit hacia 30%+ (actual ~28.2%).
2. E2E verify TOTP completo si se estabiliza secreto en CI (`E2E_2FA_TOTP_SECRET`).
3. Más páginas frontend edit/update restantes (opcional).



## Comandos locales



```bash

cd ErpProject/backend && dotnet test

cd ErpProject/frontend && npm test              # sin cobertura (rápido)
cd ErpProject/frontend && npm run test:coverage # con gate umbral

cd ErpProject/frontend && npm run test:e2e   # requiere frontend :3000

# Gate backend completo (bash + Python 3):

bash ErpProject/scripts/run-backend-coverage-gate.sh

docker compose up -d --build                 # stack completo para E2E manual

```


# ADR-0019: Roadmap producto (#38–#42f)

## Estado
Aceptado — **jul 2026**: ítems #38–#42, #42a (fases 2–5) implementados en código; resto documentado.

## Contexto
ADR-0018 ítems 38–42f describen capacidades de producto. Este ADR centraliza el diseño y el endpoint `GET /api/platform/product-roadmap`.

## Decisión

### Endpoint metadatos
- `PlatformController` → `GetProductRoadmapQuery` (MediatR).
- Devuelve lista de ítems con `id`, `title`, `status`, `blocker`, `designDoc`.

### Ítems y estado (jul 2026)

| ID | Tema | Estado código | Bloqueo externo |
|---|---|---|---|
| #38 | Multi-moneda Billing↔Treasury | ✅ Implementado | FacturaE/XML siempre EUR; homologación multi-divisa AEAT |
| #39 | Portal autoservicio cliente | Parcial (#39 portal factura) | Auth externa + UX |
| #40 | IA / anomalías | ✅ Heurísticas + LLM opcional (`Ai:Enabled`) | — |
| #41 | Onboarding guiado | ✅ CSV + Excel (.xlsx) | — |
| #42 | Notificaciones proactivas | ✅ Email + Web Push VAPID (`WebPush:Enabled`) | — |
| #42a | Gestoría multi-empresa | ✅ Fases 1–5 + line items Stripe por empresa | UI roles fase 3 refinamiento |
| #42b | Conciliación TPV/Bizum/caja | ✅ | — |
| #42c | Módulo × permiso | ✅ | — |
| #42d | Biblioteca documentos | Parcial | Storage S3 producción |
| #42e | Export periódico gestoría | ✅ Implementado | ZIP: IVA + asientos + PDFs + LEEME |
| #42f | Servicios recurrentes | ✅ | — |

### PSD2 / Open Banking (#28, relacionado)
- `ConfigurableOpenBankingProvider` (GoCardless/Nordigen) con OAuth `token/new/` cuando hay credenciales.
- Documentación: `docs/open-banking-psd2.md`.
- **Bloqueado:** contrato agregador, consentimiento redirect PSD2, homologación banco.

### Multi-moneda (#38)
- `Invoice.CurrencyCode`, `ExchangeRateToEur`, `TotalEur`.
- `IExchangeRateLookup` → Treasury `ExchangeRateService`.
- Asientos contables y cobros en EUR al bloquear/pagar.
- Frontend: selector divisa en `BillingClient`.

### Gestoría (#42a fases 2–5)
- Fase 2: `GET /api/gestoria/dashboard`, KPIs agregados en `/gestoria`.
- Fase 3: `PUT /api/gestoria/memberships/{id}/role`, selector Admin/Contable en `GestoriaClient.tsx`.
- Fase 4: `Plan.MaxCompanies`; plan **Gestoría** (15 empresas);
  `ICompanyMembershipLimitService`; UI límites en suscripción.
- Fase 5 (avanzado jul 2026): `GestoriaStripeBilling` — checkout con
  `quantity` = empresas activas; sync en webhooks; metadata
  `gestoriaBreakdownJson`; desglose en UI/historial alineado con factura
  Stripe (precio unitario × N). **Límite:** PDF Stripe = un line item con
  cantidad N, no N líneas con precio distinto por empresa.

### Export gestoría (#42e)
- ZIP: `libros-iva/`, `asientos/`, `facturas/`, `gastos/`, `LEEME.txt`.
- Filtrado por mes/trimestre (`FiscalExportPeriod`).
- `AccountantExportJob` (Hangfire día 3); UI: `settings/accountant-export`.

## Evaluación de calidad arquitectónica
- Controllers delgados MediatR; sin mocks de negocio en producción.
- Cross-módulo vía eventos (`InvoiceCardPaymentRequestedEvent`) sin referencias incorrectas.

## Consecuencias
- Fase 5 gestoría: facturación por cantidad en Stripe implementada;
  homologación TPV físico y PSD2 producción requieren terceros.

# ADR-0019: Roadmap producto (#38–#42f)

## Estado
Aceptado — **jul 2026**: ítems #38, #41 (parcial), #42a (fase 4), #42e implementados en código; resto documentado.

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
| #40 | IA / anomalías | OCR Expenses real; IA no | Proveedor + coste |
| #41 | TPV físico | ✅ Parcial | `PosTerminal`, cobro card; homologación pasarela/hardware |
| #42 | Notificaciones proactivas | Motor reglas parcial | Canal email/push |
| #42a | Gestoría multi-empresa | ✅ Fase 1+4 | Fase 5 facturación Stripe consolidada |
| #42b | Conciliación TPV/Bizum/caja | ✅ | — |
| #42c | Módulo × permiso | ✅ | — |
| #42d | Biblioteca documentos | Parcial | Storage S3 producción |
| #42e | Export periódico gestoría | ✅ Implementado | SMTP producción; PDFs en ZIP futuro |
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

### Gestoría (#42a fase 4)
- `Plan.MaxCompanies`; plan **Gestoría** (15 empresas).
- `ICompanyMembershipLimitService` valida en `AddCompanyFromAccount`.
- UI: `settings/subscription` muestra empresas usadas / límite.

### Export gestoría (#42e)
- `ExportAccountantPackageCommand`, `AccountantExportJob` (Hangfire día 3).
- Settings en `Company`: email, frecuencia.
- API: `api/accountant-export/*`; UI: `settings/accountant-export`.

## Evaluación de calidad arquitectónica
- Controllers delgados MediatR; sin mocks de negocio en producción.
- Cross-módulo vía eventos (`InvoiceCardPaymentRequestedEvent`) sin referencias incorrectas.

## Consecuencias
- Fase 5 gestoría (una factura Stripe por gestoría) sigue bloqueada por producto.
- Homologación TPV físico y PSD2 producción requieren terceros.

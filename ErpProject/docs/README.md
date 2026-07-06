# Documentación técnica — ERP SaaS España

Esta carpeta documenta la estructura real del proyecto **como Architecture
Decision Records (ADR)**: un archivo por módulo o pieza transversal,
generado leyendo el código actual (controllers, entidades, páginas de
frontend), no un plan a futuro. El objetivo es tener, para cada módulo, un
documento fiable con su flujo de punta a punta, su relación con otros
módulos y sus reglas de negocio — que se pueda consultar como contexto
antes de tocar código en ese módulo.

## Cómo están numerados

- `0000-template.md` — plantilla en blanco para futuros ADR (nuevos módulos
  o decisiones de arquitectura relevantes).
- `0001`-`0003` — arquitectura general, autenticación/multi-tenant y
  despliegue: transversales a todos los módulos de negocio.
- `0004`-`0017` — un ADR por módulo de negocio o pieza de plataforma.
- `0018` — auditoría transversal de calidad arquitectónica (SOLID, Clean
  Architecture, CQRS, duplicación, escalabilidad): la metodología que la
  sección "Evaluación de calidad arquitectónica" de cada ADR aplica.
- `0019`-`0020` — roadmap producto y Docker Compose local vs producción.

Cada ADR sigue la misma estructura: Estado, Contexto, Decisión (Backend /
Frontend / Modelo de datos / Flujo end-to-end), Relación con otros módulos,
Evaluación de calidad arquitectónica, Buenas prácticas aplicables y
Consecuencias. Cuando el código tiene huecos, deuda técnica o partes sin
conectar, el ADR lo dice explícitamente en vez de asumir que todo funciona —
varios módulos tienen hallazgos así (ver tabla).

## Índice

| ADR | Módulo / pieza | Notas relevantes |
|---|---|---|
| [0001](adr/0001-arquitectura-general.md) | Arquitectura general | Modular monolith + Clean Architecture, CQRS/MediatR, patrón Outbox. ~878 tests (backend + frontend + E2E); la violación de dirección de dependencias (`Erp.Infrastructure`→módulos Application) se reabrió una vez tras el cierre inicial y ya está corregida de nuevo, con un test de dos capas que la protege (ver ADR-0018 ítem 13) |
| [0002](adr/0002-multitenancy-auth.md) | Multi-tenancy y autenticación | JWT+refresh, 2FA, RBAC/ABAC, `CompanyId` como aislamiento de tenant |
| [0003](adr/0003-despliegue-infraestructura.md) | Despliegue e infraestructura | Health-check post-deploy roto (puerto equivocado), TLS desactivado con HSTS activo, Postgres expuesto a internet — ver ADR-0018 |
| [0004](adr/0004-crm.md) | CRM | Clients, Contacts, Leads, Suppliers, Notes, Alerts |
| [0005](adr/0005-billing.md) | Billing (Facturación) | Invoices, Quotes, FacturaE, hash-chain, normativa antifraude |
| [0006](adr/0006-accounting.md) | Accounting (Contabilidad) | Controllers antes mock (AEAT, VAT, VIES, Prorrata...) reescritos con MediatR real; `POST .../vat/declare/modelo330` registra autoliquidación trimestral real (`VatLiquidation` vía `DeclareModelo330Command`, nombre legacy 330 → modelo 303); export periódico para gestoría (ZIP libro IVA) |
| [0007](adr/0007-expenses.md) | Expenses | Captura OCR (Tesseract) de tickets vía QR público |
| [0008](adr/0008-inventory.md) | Inventory | Productos, Stock, Lotes/Series; movimientos de stock reales desde Billing, Expenses, Sales y Purchasing; el doble descuento de stock en el ciclo completo de venta ya está corregido (ver ADR-0018 ítem 66) |
| [0009](adr/0009-payroll.md) | Payroll (Nóminas) | CQRS/MediatR completo (`PayrollController` solo `IMediator`); frontend rotulado "Fase 0" a propósito (alcance inicial deliberado, no desconexión); export RED/SILTRA orientativo, no homologado TGSS |
| [0010](adr/0010-purchasing.md) | Purchasing | Flujo de aprobación de pedidos real; integración real con Inventory y con Accounting (three-way match → asiento 600/472/410, ADR-0018 ítem 69); formularios de recepción/factura de proveedor conectados a la UI real; queda pendiente el vínculo `SupplierId`↔CRM |
| [0011](adr/0011-sales.md) | Sales | Integración real con CRM, Inventory y Billing (reutiliza el pipeline fiscal real, no duplica facturación); el doble descuento de stock compartido con Inventory ya está corregido (ver ADR-0008 y ADR-0018 ítem 66) |
| [0012](adr/0012-treasury.md) | Treasury | Conciliación bancaria retrospectiva contra asientos de Accounting; Open Banking real (GoCardless) con fallback a Mock; TPV físico con backend y frontend conectados (ADR-0018 ítem 72) |
| [0013](adr/0013-fiscal-sii-verifactu.md) | Fiscal — SII y VeriFactu | Cumplimiento normativo español, cruza con Billing y Accounting |
| [0014](adr/0014-suscripciones-licencias.md) | Suscripciones y Licencias | Billing del propio SaaS (Stripe), distinto de ADR-0005; plan Gestoría con límite de empresas (`MaxCompanies`) |
| [0015](adr/0015-automatizacion.md) | Automatización | Motor de reglas funcional: persiste reglas reales, `RuleEvaluatorJob` diario + evaluación en tiempo real conectados |
| [0016](adr/0016-api-publica-keys.md) | API Pública y API Keys | Sistema unificado de API Keys con rate limiting; el sistema Redis huérfano en paralelo ya se eliminó |
| [0017](adr/0017-audit-logs.md) | Audit Logs | Interceptor de auditoría conectado en los 9 módulos (`CrmDbContext` era la excepción, corregido — ver ADR-0018 ítem 71) |
| [0018](adr/0018-calidad-arquitectura.md) | Calidad arquitectónica (transversal) | Backlog 64 ítems; techo accionable ~100%; ver sección Cierre backlog |
| [0019](adr/0019-producto-roadmap.md) | Roadmap producto (#38–#42f) | 9 de 11 ítems implementados en código (#38/#40/#41/#42/#42a/#42b/#42c/#42e/#42f ✅); parciales #39 (portal cliente) y #42d (biblioteca documentos, falta S3 producción) |
| [0020](adr/0020-docker-local-produccion.md) | Docker Compose local vs producción | Comandos, URLs, migraciones, seed, `.env`, troubleshooting — complementa ADR-0003 |

## Cómo usar esta carpeta

Antes de implementar o modificar algo en un módulo concreto, lee su ADR
correspondiente para conocer su estructura real, sus dependencias con otros
módulos y sus huecos conocidos. Si el trabajo cambia la estructura descrita
en un ADR (nuevo endpoint, nueva entidad, nueva integración), actualiza ese
ADR en el mismo cambio para que siga siendo fuente de verdad.

Para crear un ADR nuevo (módulo nuevo o decisión de arquitectura
significativa), copia `adr/0000-template.md`, numéralo secuencialmente y
añádelo a la tabla de arriba.

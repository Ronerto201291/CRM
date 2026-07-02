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

Cada ADR sigue la misma estructura: Estado, Contexto, Decisión (Backend /
Frontend / Modelo de datos / Flujo end-to-end), Relación con otros módulos,
Evaluación de calidad arquitectónica, Buenas prácticas aplicables y
Consecuencias. Cuando el código tiene huecos, deuda técnica o partes sin
conectar, el ADR lo dice explícitamente en vez de asumir que todo funciona —
varios módulos tienen hallazgos así (ver tabla).

## Índice

| ADR | Módulo / pieza | Notas relevantes |
|---|---|---|
| [0001](adr/0001-arquitectura-general.md) | Arquitectura general | Modular monolith + Clean Architecture, CQRS/MediatR, patrón Outbox. Sin tests automatizados en todo el repo. |
| [0002](adr/0002-multitenancy-auth.md) | Multi-tenancy y autenticación | JWT+refresh, 2FA, RBAC/ABAC, `CompanyId` como aislamiento de tenant |
| [0003](adr/0003-despliegue-infraestructura.md) | Despliegue e infraestructura | Docker Compose (VPS Hetzner) y manifiesto k8s en paralelo; CI/CD |
| [0004](adr/0004-crm.md) | CRM | Clients, Contacts, Leads, Suppliers, Notes, Alerts |
| [0005](adr/0005-billing.md) | Billing (Facturación) | Invoices, Quotes, FacturaE, hash-chain, normativa antifraude |
| [0006](adr/0006-accounting.md) | Accounting (Contabilidad) | Varios controllers (AEAT, VAT, VIES, Prorrata...) devuelven datos mock, sin persistencia real |
| [0007](adr/0007-expenses.md) | Expenses | Captura OCR (Tesseract) de tickets vía QR público |
| [0008](adr/0008-inventory.md) | Inventory | Productos, Stock, Lotes/Series; movimientos de stock solo desde Billing y Expenses |
| [0009](adr/0009-payroll.md) | Payroll (Nóminas) | Sin capa CQRS/MediatR, a diferencia del resto; frontend marcado "Fase 0" |
| [0010](adr/0010-purchasing.md) | Purchasing | Three-way match; sin integración real con Inventory ni Accounting |
| [0011](adr/0011-sales.md) | Sales | Pedidos/Entregas/Facturas de cliente; sin integración real con Inventory ni Billing |
| [0012](adr/0012-treasury.md) | Treasury | Conciliación bancaria retrospectiva contra asientos de Accounting |
| [0013](adr/0013-fiscal-sii-verifactu.md) | Fiscal — SII y VeriFactu | Cumplimiento normativo español, cruza con Billing y Accounting |
| [0014](adr/0014-suscripciones-licencias.md) | Suscripciones y Licencias | Billing del propio SaaS (Stripe), distinto de ADR-0005 |
| [0015](adr/0015-automatizacion.md) | Automatización | Motor de reglas no funcional: no persiste, ni frontend ni job están conectados |
| [0016](adr/0016-api-publica-keys.md) | API Pública y API Keys | Dos sistemas de API Key en paralelo y desconectados |
| [0017](adr/0017-audit-logs.md) | Audit Logs | El interceptor que debería auditar cambios nunca se invoca |
| [0018](adr/0018-calidad-arquitectura.md) | Calidad arquitectónica (transversal) | Backlog de remediación (5/19 corregidos); catálogo completo de datos mock; credenciales hardcodeadas documentadas aparte (última prioridad) |

## Cómo usar esta carpeta

Antes de implementar o modificar algo en un módulo concreto, lee su ADR
correspondiente para conocer su estructura real, sus dependencias con otros
módulos y sus huecos conocidos. Si el trabajo cambia la estructura descrita
en un ADR (nuevo endpoint, nueva entidad, nueva integración), actualiza ese
ADR en el mismo cambio para que siga siendo fuente de verdad.

Para crear un ADR nuevo (módulo nuevo o decisión de arquitectura
significativa), copia `adr/0000-template.md`, numéralo secuencialmente y
añádelo a la tabla de arriba.

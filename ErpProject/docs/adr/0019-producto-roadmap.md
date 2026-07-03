# ADR-0019: Roadmap producto (#38–#42f)

## Estado
Aceptado — diseño y stubs documentados; **no implementar sin OK de producto**.

## Contexto
ADR-0018 ítems 38–42f describen capacidades de producto que requieren
decisiones de negocio, contratos con terceros o homologación regulatoria.
El código actual no contiene implementaciones parciales accionables para la
mayoría de ellos (confirmado por barrido jul 2026). Este ADR centraliza el
diseño propuesto y el endpoint de metadatos `GET /api/platform/product-roadmap`.

## Decisión

### Endpoint stub (solo metadatos)
- `PlatformController` → `GetProductRoadmapQuery` (MediatR).
- Devuelve lista de ítems con `id`, `title`, `status`, `blocker`, `designDoc`.
- **No** expone endpoints de negocio simulados (PSD2, TPV, etc.).

### Ítems y estado honesto

| ID | Tema | Estado código | Bloqueo |
|---|---|---|---|
| #38 | Multi-moneda avanzada Billing↔Treasury | No iniciado | Conversión automática y redondeo por divisa |
| #39 | Portal autoservicio cliente/proveedor | No iniciado | Auth externa + modelo UX |
| #40 | IA (anomalías, previsión, categorización) | OCR Expenses real; IA no | Proveedor + coste |
| #41 | Onboarding guiado / import ERP | No iniciado | Plantillas sectoriales |
| #42 | Notificaciones proactivas | Motor reglas parcial (#27) | Canal email/push + plantillas |
| #42a | Gestoría multi-empresa Fase 2+ | Fase 1 ✅ (ADR-0002) | Modelo suscripción N empresas |
| #42b | Conciliación TPV/Bizum/caja | No iniciado | Entidades arqueo + pasarela |
| #42c | Módulo contratado × permiso usuario | ABAC parcial | Unificar con ModuleAuthorization |
| #42d | Biblioteca documentos | No iniciado | Entidad `Document` + storage |
| #42e | Export periódico a gestoría externa | No iniciado | Alcance ZIP/email |
| #42f | Servicios recurrentes por cliente | No iniciado | Catálogo servicios + job facturación |

### Homologación fiscal (relacionado #39)
Ver ADR-0013 y `GET /api/fiscal/homologation/status` — preparatorio sin cert AEAT.

## Relación con otros módulos
- **#42a:** ADR-0002 (auth multi-empresa).
- **#38:** ADR-0012 Treasury (tipos de cambio) + ADR-0005 Billing.
- **#42f:** ADR-0004 CRM + ADR-0005 Billing + ADR-0006 Accounting.

## Evaluación de calidad arquitectónica
- Roadmap vía MediatR; sin lógica de negocio falsa en controllers.
- Frontend puede consumir `/api/platform/product-roadmap` para mostrar estado.

## Consecuencias
- Ningún ítem de esta tabla se implementará sin OK explícito de producto/legal.
- ADR-0018 marca estos ítems como **requiere OK producto** en la sección Cierre backlog.

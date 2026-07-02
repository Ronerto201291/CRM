# CRM / ERP SaaS España

Este repo aloja un **ERP SaaS para España** en `ErpProject/` (no un CRM
aislado — CRM es uno de nueve módulos de negocio). Backend en .NET 10
(Clean Architecture + CQRS/MediatR, monolito modular) en
`ErpProject/backend/`, frontend en Next.js 15 (App Router) en
`ErpProject/frontend/`.

## Antes de tocar un módulo, lee su ADR

La estructura real de cada módulo (controllers, entidades, páginas,
integraciones con otros módulos, huecos conocidos) está documentada en
`ErpProject/docs/adr/`. Lee el ADR correspondiente antes de implementar o
modificar algo — varios módulos tienen deuda técnica ya identificada
(código muerto, integraciones a medias, endpoints mock) que el ADR señala
explícitamente para no asumir que todo funciona.

Índice completo: `ErpProject/docs/README.md`.

| Módulo / tema | ADR |
|---|---|
| Arquitectura general (modular monolith, CQRS, Outbox) | `ErpProject/docs/adr/0001-arquitectura-general.md` |
| Auth, usuarios, roles, multi-tenant | `ErpProject/docs/adr/0002-multitenancy-auth.md` |
| Despliegue (Docker/k8s/VPS/CI-CD) | `ErpProject/docs/adr/0003-despliegue-infraestructura.md` |
| CRM (clientes, leads, proveedores) | `ErpProject/docs/adr/0004-crm.md` |
| Billing (facturas, presupuestos, FacturaE) | `ErpProject/docs/adr/0005-billing.md` |
| Accounting (contabilidad, IVA, AEAT) | `ErpProject/docs/adr/0006-accounting.md` |
| Expenses (gastos, OCR) | `ErpProject/docs/adr/0007-expenses.md` |
| Inventory (stock, almacenes) | `ErpProject/docs/adr/0008-inventory.md` |
| Payroll (nóminas) | `ErpProject/docs/adr/0009-payroll.md` |
| Purchasing (compras) | `ErpProject/docs/adr/0010-purchasing.md` |
| Sales (pedidos, entregas, facturas de cliente) | `ErpProject/docs/adr/0011-sales.md` |
| Treasury (tesorería, divisas, financiación) | `ErpProject/docs/adr/0012-treasury.md` |
| Fiscal, SII, VeriFactu | `ErpProject/docs/adr/0013-fiscal-sii-verifactu.md` |
| Suscripciones y licencias (Stripe) | `ErpProject/docs/adr/0014-suscripciones-licencias.md` |
| Automatización (motor de reglas) | `ErpProject/docs/adr/0015-automatizacion.md` |
| API pública y API Keys | `ErpProject/docs/adr/0016-api-publica-keys.md` |
| Audit Logs | `ErpProject/docs/adr/0017-audit-logs.md` |
| Calidad arquitectónica (SOLID, Clean Architecture, CQRS, duplicación) | `ErpProject/docs/adr/0018-calidad-arquitectura.md` |

Si un cambio modifica la estructura descrita en un ADR (nuevo endpoint,
entidad, integración), actualiza ese ADR en el mismo cambio.

## Antes de dar por terminado un cambio, pasa el checklist de calidad

Todo ADR incluye una sección **"Evaluación de calidad arquitectónica"**
(plantilla en `0000-template.md`, metodología completa en `ADR-0018`). Antes
de considerar terminada una implementación en un módulo:
- Los controllers nuevos/modificados son delgados: construyen un
  Command/Query y llaman a `_mediator.Send(...)`, sin lógica de negocio ni
  acceso a datos inline (24 de 43 controllers todavía incumplen esto — no
  sumar más; ver backlog de remediación en ADR-0018).
- No se duplica lógica que ya existe en un Command/Handler (patrón repetido
  y ya corregido en VIES, VAT y Prorrata — ver ADR-0018 §4 antes de crear un
  segundo cálculo/consulta que ya exista).
- Si el módulo usa CQRS, cualquier flujo nuevo pasa por MediatR; si el
  módulo no tiene CQRS (caso Payroll), no asumir que existe sin comprobar.
- Los endpoints de listado paginan; no se añaden queries dentro de bucles.
- Las referencias de proyecto nuevas respetan la dirección de dependencias
  (Api→Application→Domain; el core no depende de módulos).
- **El frontend de la página tocada queda conectado de verdad al backend**:
  si arreglas o implementas un endpoint, confirma que la página que lo usa
  hace `fetch`/`onClick` real contra él (no lo des por hecho — hay páginas
  enteras que son mock puro, ver ADR-0018 catálogo de mock). No se cierra un
  cambio dejando el backend arreglado pero el frontend correspondiente
  todavía desconectado o con botones sin `onClick`.

## Comandos básicos

```bash
# Backend (.NET)
cd ErpProject/backend
dotnet restore && dotnet build
dotnet run --project Erp.Api        # dev server + swagger en :5000

# Frontend (Next.js)
cd ErpProject/frontend
npm install
npm run dev                          # :3000
npm run lint

# Stack completo
cd ErpProject
docker compose up -d                 # frontend :3000, backend+swagger :5000
```

## Huecos conocidos

- No hay tests automatizados en todo el repo (ni `*.Tests.csproj` en
  backend, ni runner JS configurado en frontend).
- `ErpProject/backend/Erp.slnx` no registra explícitamente todos los
  módulos que sí están cableados en `Program.cs` (ver ADR-0001).
- Varios módulos tienen partes construidas pero no conectadas end-to-end
  (ver la columna "Notas relevantes" en `ErpProject/docs/README.md`).

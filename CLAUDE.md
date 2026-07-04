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

Al cerrar un ítem en ADR-0018 o modificar un módulo, actualiza también
**Decisión**, **Consecuencias** y **Evaluación de calidad** del ADR del
módulo (no solo una nota al final); elimina frases obsoletas («sin
IMediator», «mock», «código muerto», entidades en `Erp.Domain`) y comprueba
con `0000-template.md` que no contradigan ADR-0018 ni el código.

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
- **No regresión sobre ítems ya cerrados**: ADR-0018 mantiene una lista de
  ítems marcados ✅ Corregido (deuda ya arreglada y verificada). Antes de
  dar por terminado cualquier cambio nuevo, comprueba que no reintroduce un
  problema ya cerrado (ej.: no volver a duplicar un cálculo que ya se
  unificó, no repetir un `ProjectReference` en la dirección incorrecta que
  ya se corrigió, no romper un merge de Compose ya arreglado). Si un cambio
  toca un archivo que aparece como evidencia de un ítem ✅ Corregido,
  reléelo antes de modificarlo. El objetivo es que la deuda técnica sea
  monótonamente decreciente: cada corrección debe quedar protegida, no
  solo hecha una vez.
- **Toda funcionalidad nueva lleva test que corra en CI, sin excepción**:
  ningún handler, entidad, endpoint o componente nuevo se da por terminado
  sin al menos un test que lo cubra en `backend/tests/` (`Erp.Tests` para
  unit, `Erp.IntegrationTests` para flujos con BBDD real,
  `Erp.ArchitectureTests` si añade una regla estructural) y que
  `dotnet test ErpProject/backend/Erp.slnx` lo ejecute en verde en el mismo
  cambio — no en un PR aparte "de tests" posterior. Igual para frontend en
  cuanto exista runner (ver huecos conocidos). Esto aplica también a bugs
  corregidos: el test que prueba la corrección es lo que impide que el bug
  vuelva (refuerza la regla de no regresión de arriba). No añadir un
  test que solo repita lo que el propio código ya afirma (ver el caso de
  `SpanishTaxIdValidatorTests.FindValidCif`, ADR-0018 ítem 0f) — el test
  tiene que verificar contra un caso conocido/externo, no contra el mismo
  código que pretende probar.
- **Migraciones de EF Core generadas en paralelo por dos sesiones pueden
  quedar desordenadas entre sí**: `dotnet ef migrations add` genera el
  timestamp del nombre de archivo a partir de la hora local del momento en
  que se ejecuta, no de un orden lógico de dependencias. Si dos cambios en
  paralelo tocan el mismo tipo de entidad (uno añade una columna vía código
  +migración, otro genera su propia migración sin haber tirado antes esa
  migración ajena), la migración de quien la generó antes puede acabar
  con un `AlterColumn`/referencia a una columna que otra migración —con
  timestamp posterior— es la que de verdad la crea; en una base de datos
  nueva esto revienta con "column ... does not exist" (pasó de verdad:
  `AddDocumentTable` vs `AddMatchingToleranceAmountToCompany`). Antes de
  hacer `git push` de cualquier migración nueva: `git fetch`/`pull` primero
  para tener las migraciones más recientes de la otra sesión, y verificar
  el orden con `dotnet ef migrations script --idempotent` (no necesita una
  BBDD real) buscando que cada columna se cree antes de que otra migración
  la altere.

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

# Stack completo (local, con BBDD en Docker — sin servidor)
cd ErpProject
cp .env.example .env                  # rellena POSTGRES_PASSWORD y JWT_SECRET
docker compose up -d --build        # local: override.yml se carga automáticamente
# frontend http://localhost (nginx :80) o http://localhost:3000 (directo); backend+swagger :8081, postgres :5432
# producción: docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

## Huecos conocidos

- Cobertura de tests **muy parcial**: backend tiene 3 proyectos en
  `backend/tests/` (`Erp.Tests`, `Erp.IntegrationTests`,
  `Erp.ArchitectureTests`, ~61 casos, CI con `dotnet test`); frontend sin
  runner (`npm test` no existe). Ver ADR-0018 ítem #32.
- `ErpProject/backend/Erp.slnx` no registra explícitamente todos los
  módulos que sí están cableados en `Program.cs` (ver ADR-0001).
- Varios módulos tienen partes construidas pero no conectadas end-to-end
  (ver la columna "Notas relevantes" en `ErpProject/docs/README.md`).

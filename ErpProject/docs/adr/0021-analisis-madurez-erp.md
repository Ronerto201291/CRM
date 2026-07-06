# ADR-0021: Análisis de madurez del ERP (julio 2026)

## Estado
Aceptado — análisis verificado contra código y ADRs existentes en `main` (julio 2026).

## Contexto

Este ADR consolida un **análisis de madurez integral** de ErpProject realizado en julio 2026,
contrastando la documentación existente (`docs/adr/`, especialmente
[ADR-0018](0018-calidad-arquitectura.md) y [ADR-0019](0019-producto-roadmap.md)) con el
código en `ErpProject/backend/` y `ErpProject/frontend/`.

A diferencia de los ADR por módulo (0004–0017), que documentan la estructura as-built de
cada pieza, este documento responde a la pregunta transversal: **¿en qué punto está el
producto frente a un ERP SaaS competitivo para PYMES españolas, tanto en código como en
cumplimiento legal y operación comercial?**

El análisis cubre los 9 módulos de negocio más fiscal, auth, automatización, API pública,
suscripciones y plataforma. Referencias cruzadas obligatorias:

| ADR | Relación |
|-----|----------|
| [0001](0001-arquitectura-general.md) | Arquitectura modular monolith, CQRS, Outbox |
| [0002](0002-multitenancy-auth.md) | Auth, multi-tenant, ABAC |
| [0003](0003-despliegue-infraestructura.md) | Infra producción aparcada |
| [0004](0004-crm.md) – [0014](0014-suscripciones-licencias.md) | Módulos de negocio y plataforma |
| [0013](0013-fiscal-sii-verifactu.md) | Cumplimiento fiscal SII/VeriFactu |
| [0018](0018-calidad-arquitectura.md) | Backlog calidad, madurez dual ~92% / ~58% |
| [0019](0019-producto-roadmap.md) | Roadmap #38–#42f mayormente implementado |
| [0020](0020-docker-local-produccion.md) | Compose local OK; prod pendiente |

**Fecha del análisis:** julio 2026.

## Decisión

Este ADR **no introduce una decisión de implementación nueva**. Registra el estado de
madurez verificado y las brechas priorizadas como fuente de verdad para planificación
de producto, go-to-market y certificación legal.

### Resumen ejecutivo

**ErpProject** es un ERP SaaS multi-tenant para PYMES españolas con backend .NET 10
(monolito modular, CQRS/MediatR, 9 módulos de negocio + fiscal/auth/plataforma) y
frontend Next.js 15 (~86 rutas en `frontend/src/app/`). La documentación en `docs/adr/`
es excepcionalmente fiel al código: no describe aspiraciones, sino lo verificado archivo
por archivo.

En **madurez de código/arquitectura** el proyecto está muy avanzado (~92% según
ADR-0018): controllers delgados, comunicación entre módulos por eventos/outbox, ABAC por
módulo y permiso, ~694+ tests unitarios + ~164 integración + gates de cobertura en CI,
Docker Compose local funcional, y la mayoría de páginas frontend conectadas al backend
real (el catálogo de mock de ADR-0018 está prácticamente vacío).

En **madurez comercial y cumplimiento legal en producción** la foto es más conservadora
(~55–58%): el código fiscal (VeriFactu, SII, FacturaE, modelos 303/347) es real y ha
pasado varias rondas de corrección frente a especificaciones AEAT, pero **la
homologación/certificación en entornos oficiales sigue bloqueada externamente**
(certificados FNMT, entorno test AEAT, FACe, entidades bancarias SEPA, TGSS/SILTRA). Sin
esa homologación, un competidor como Holded o Sage puede vender "cumplimiento
garantizado"; aquí el cumplimiento es "implementado en código, pendiente de validación
oficial".

Las **fortalezas diferenciadoras** frente a un ERP genérico son: captura inteligente de
gastos (QR + OCR Tesseract local), cadena antifraude y VeriFactu, integración
order-to-cash (Sales → Inventory → Billing → Accounting), portal cliente (ver/pagar
factura, subida proveedor), gestoría multi-empresa con Stripe por cantidad, automatización
de reglas, y export ZIP periódico para gestorías.

Las **brechas principales** para competir con un ERP español maduro son: nóminas en "Fase
2" sin homologación TGSS, presentación telemática AEAT no cerrada end-to-end,
almacenamiento documental en producción (S3), despliegue en servidor aparcado, y huecos
funcionales puntuales (rechazo de gastos sin endpoint, conversión manual de facturas de
proveedor subidas por portal, HR ausente).

### Mapa de madurez por módulo

| Módulo | Madurez (1-5) | End-to-end | Principal gap |
|--------|---------------|------------|---------------|
| **Auth / Multi-tenant** | 4 | Sí | Super-admin atado a email fijo; RLS no en todas las tablas |
| **CRM** | 4 | Sí | Sin pipeline comercial avanzado (oportunidades, scoring, email marketing) |
| **Billing** | 4 | Sí | Homologación VeriFactu/FACe en producción; multi-divisa en FacturaE siempre EUR |
| **Accounting** | 3,5 | Sí (mayoría) | Exports orientativos; presentación telemática AEAT; EFE simplificado |
| **Expenses** | 4 | Casi | Botón "Rechazar" en UI sin `POST /reject` en backend |
| **Inventory** | 3,5 | Sí | Sin MRP/fabricación; valoración PMP/FIFO básica |
| **Sales** | 4 | Sí | Etiquetas de estado UI ≠ backend; sin CRM activity en pedidos |
| **Purchasing** | 4 | Sí | Factura proveedor subida por portal → conversión manual a `SupplierInvoice` |
| **Treasury** | 3,5 | Sí | PSD2/Open Banking real requiere contrato agregador; SEPA sin homologación banco |
| **Payroll** | 2 | Sí (alcance limitado) | Sin SILTRA/TGSS homologado; sin vacaciones/bajas/finiquitos |
| **Fiscal (SII/VeriFactu)** | 3 | Sí (código) | Homologación AEAT; `Sii:SendEnabled` desactivado por defecto |
| **Automatización** | 4 | Sí | Reglas potentes pero acciones limitadas (email/push) |
| **API pública** | 4 | Sí | Sin SDK/documentación OpenAPI pública extensa |
| **Suscripciones** | 4 | Sí | Modelo de negocio gestoría aún con decisiones pendientes |
| **Plataforma (docs, audit, onboarding)** | 4 | Sí | Biblioteca documentos: MinIO local OK, S3 producción pendiente (#42d) |

### Brechas funcionales (por área de negocio)

#### CRM (`docs/adr/0004-crm.md`)

**Implementado end-to-end:** clientes, proveedores, contactos, leads, notas, alertas,
anonimización RGPD, catálogo de servicios y facturación recurrente (#42f), portal subida
proveedor, paginación, tests.

**A medias / limitado:**

- `crm/prospects` reutiliza leads (funcional, no módulo separado).
- Timeline CRM no registra creación de pedidos Sales automáticamente.

**Faltaría vs ERP competitivo España:** campañas, oportunidades con probabilidad/importe,
integración email/calendario, scoring, segmentación avanzada, duplicados inteligentes,
integración con firma electrónica de contratos.

#### Billing (`docs/adr/0005-billing.md`)

**Implementado:** presupuestos con portal público, facturas con numeración correlativa,
hash chain, bloqueo, VeriFactu (huella, QR, anulaciones, conservación local, logs),
rectificativas, IRPF, recargo equivalencia, multi-moneda (#38), cobro con métodos
TPV/Bizum/caja, portal cliente con Stripe Checkout, FacturaE XML/XAdES, PDF con leyenda
VERI*FACTU.

**A medias:**

- FacturaE: bloque `Extensions` mal formado (ADR-0013); homologación FACe pendiente.
- Modo `LocalOnly` VeriFactu existe pero la decisión de producto "qué modo vender" queda
  al cliente.

**Faltaría:** recepción FacturaE entrante (Crea y Crece B2B completo), series múltiples
avanzadas, facturación masiva, plantillas PDF personalizables, remesas de cobro SEPA
cliente, factura proforma vs definitiva, integración TBAI/otras CCAA si se amplía
geográficamente.

#### Accounting (`docs/adr/0006-accounting.md`)

**Implementado:** PGC sembrado automáticamente al crear empresa (#0g), diario, mayor, PyG,
balance, cierre ejercicio, libros IVA, modelos 303/347/111/190/349/390 (exports),
prorrata, recargo, VIES, ISP, activos fijos, provisiones, periodificaciones,
presupuestos, centros de coste, aging real, export gestoría ZIP (#42e).

**A medias:**

- Exports marcados `MarkAsNonOfficial` — orientativos, no sustituyen Sede AEAT.
- `DeclareModelo330Command` persiste liquidación pero no presenta telemáticamente.
- Estados financieros EFE/patrimonio: implementados desde asientos pero con heurísticas
  simplificadas.

**Faltaría:** presentación directa modelos AEAT (certificado + Sede), contabilidad
analítica avanzada, consolidación normativa (no solo Treasury), cierre automático con
checklist, integración con software de gestoría (A3link, etc.), libro de registro de
bienes de inversión completo.

#### Expenses (`docs/adr/0007-expenses.md`)

**Implementado:** QR público, OCR Tesseract, aprobación con asiento contable + stock +
activity log, anomalías IA heurística (#40), flujo aprobación por umbral.

**Desconectado:**

- `ExpenseDetailClient.tsx` llama `POST /api/proxy/expenses/{id}/reject` pero **no
  existe** endpoint `reject` en `ExpensesController.cs` (solo `approve`).

**Faltaría:** OCR en facturas de proveedor PDF multipágina, conciliación gasto-tarjeta,
dietas/kilometraje normativa española, integración directa con bancos (no solo tesorería).

#### Inventory (`docs/adr/0008-inventory.md`)

**Implementado:** productos, almacenes, stock, movimientos, lotes, series, valoración
PMP/FIFO, integración con Billing/Expenses/Sales/Purchasing por eventos, corrección doble
descuento stock (#66).

**Faltaría:** trazabilidad reglamentaria (sanidad, pharma), inventario cíclico
programado, picking/packing, códigos de barras/ETI, integración con marketplaces,
BOM/kitting.

#### Sales (`docs/adr/0011-sales.md`)

**Implementado:** pedidos con FK a CRM, albaranes con decremento stock, facturas cliente
vía Billing real, paginación, tests.

**A medias:**

- UI filtra por `Open/Shipped/Delivered/Cancelled` pero backend usa
  `Open/PartiallyDelivered/Completed`.

**Faltaría:** presupuestos vinculados a pedido, devoluciones RMA, comisiones comerciales,
tarifas por cliente/escala, integración transportistas.

#### Purchasing (`docs/adr/0010-purchasing.md`)

**Implementado:** PO con aprobación, recepción con stock, three-way match, factura
proveedor con FK CRM, asiento contable (#69), selector proveedor en UI.

**A medias:**

- `SupplierInvoiceUpload` (portal) requiere revisión manual; no crea `SupplierInvoice`
  automáticamente.

**Faltaría:** OCR facturas recibidas → matching PO, catálogo proveedor con precios
acordados, contratos marco, evaluación proveedores.

#### Treasury (`docs/adr/0012-treasury.md`)

**Implementado:** cuentas bancarias, import CSV, conciliación contra contabilidad,
efectos, órdenes de pago, divisas/BCE, confirming/factoring, avales, consolidación de
grupo (corregida #15), arqueo caja (#42b), TPV físico conectado (#72), previsión liquidez
IA (#40).

**A medias:**

- Open Banking: `ConfigurableOpenBankingProvider` (GoCardless) con fallback Mock/Stub en
  prod sin credenciales.
- SEPA pain.001/008 generados pero sin homologación bancaria (#0d).

**Faltaría:** remesas SEPA automáticas desde facturas, confirming bancario real,
tesorería previsional con escenarios, multi-banco agregado sin agregador propio.

#### Payroll (`docs/adr/0009-payroll.md`)

**Implementado (Fase 2 deliberada):** empleados, liquidaciones, plantillas, cálculo
básico SS/IRPF, exports TC1/TC2/RED orientativos, PDF recibo orientativo, modelos
111/190 orientativos desde nómina, integración contable vía puerto.

**Explícitamente fuera de alcance:** homologación TGSS/SILTRA, bajas médicas, vacaciones,
finiquitos, contratos, convenios colectivos detallados.

**Faltaría vs Sage/A3:** nómina legalmente presentable, SILTRA, certificados de empresa,
retribución flexible, control horario (obligatorio España), nómina electrónica empleado.

#### Fiscal SII/VeriFactu (`docs/adr/0013-fiscal-sii-verifactu.md`)

**Implementado en código:** SII XML dual namespace, XAdES-BES, validación offline, envío
HTTP con mTLS si `Sii:SendEnabled=true`; VeriFactu corregido (8 campos huella, XML
tikeV1.0, anulaciones, conservación ZIP, logs); calendario fiscal; páginas `sii`,
`verifactu`, `fiscal`.

**Bloqueado externo:** homologación certificado AEAT entorno test/producción (#0b).

#### Auth, Automatización, API, Suscripciones

- **Auth (#0002):** JWT, refresh, 2FA, multi-empresa `UserCompany`, switch empresa, ABAC
  (#42c), invitaciones — sólido.
- **Automatización (#0015):** reglas BD + job diario + tiempo real — funcional.
- **API pública (#0016):** API Keys unificadas con rate limit Redis.
- **Suscripciones (#0014):** Stripe checkout/webhook/idempotencia, planes con módulos,
  gestoría `MaxCompanies` + facturación por cantidad.

### Brechas técnicas y de código

#### Arquitectura

- **Patrón correcto y mayormente cumplido:** monolito modular, 4 `.csproj` por módulo,
  `IErpModule` autoregistro (#19e), outbox transaccional, eventos cross-módulo sin
  inyectar DbContext ajeno (patrón corregido en Expenses→CRM #19d).
- **Deuda residual:** interfaces `I*DbContext` muy anchas (ISP — 29 DbSets en Accounting);
  acoplamiento Treasury↔Accounting resuelto con puertos pero exporters contables siguen
  leyendo 6 contextos en Infrastructure; añadir módulo 10º sigue siendo manual en
  convenciones aunque `IErpModule` ayuda.
- **Dirección de dependencias:** violación core→módulos corregida dos veces (#13) con
  tests `DependencyDirectionArchitectureTests` — riesgo de regresión documentado
  explícitamente.

#### Calidad y tests

- **~694 unit + ~164 integración + 7 arquitectura + ~142 Vitest + 25 Playwright**
  (ADR-0018, jul 2026).
- Gates CI: merged ~49% línea mínimo; Billing.Application 55%; Accounting.Application
  solo 18% umbral (módulo fiscal complejo, poca cobertura relativa).
- **FluentValidation:** solo CRM registra `AddValidatorsFromAssembly`; validators en otros
  módulos no se ejecutarían si se añadieran.
- **5 reglas** `ControllerArchitectureTests` + dependency direction — buena red de
  seguridad.

#### Frontend

- Catálogo mock ADR-0018 **cerrado** (facturae, iva-registers, automation conectados).
- Patrón RSC + Client en hubs; Zod en formularios `*/new` principales.
- **Bug verificado:** rechazo gastos en `expenses/[id]/ExpenseDetailClient.tsx` → endpoint
  inexistente.
- Proxy Next.js con whitelist `ALLOWED_PATH_PREFIXES` (#46) — mejorado pero sigue siendo
  superficie sensible.

#### Infra y CI/CD

- Docker Compose local verificado (#65); producción VPS **aparcada** (TLS, backups
  offsite, zero-downtime — ítems 53–64 aparcados).
- CI: build, tests, coverage gate, `npm audit`/`dotnet vulnerable`; un solo workflow
  (#59).
- `k8s/deployment.yaml` obsoleto (#62).
- Postgres RLS en 19 tablas piloto (#34); no defensa en profundidad completa.

#### Seguridad

- Credenciales dev movidas a env (#appsettings corregido).
- Pendiente: super-admin por email literal `admin@devcorp.com` en `AdminController.cs`;
  fallbacks MinIO/RabbitMQ guest.

### Cumplimiento fiscal España

| Área | Estado código | Estado legal producción |
|------|---------------|-------------------------|
| **Numeración correlativa / hash antifraude** | Implementado en Billing | Listo para uso interno |
| **VeriFactu (RD 1007/2023)** | Huella, XML, anulaciones, conservación, modo LocalOnly | Homologación TIKE pendiente (#0b externo) |
| **SII** | XML, firma, validador offline, envío opcional | Certificado + homologación AEAT pendiente |
| **FacturaE / FACe** | XAdES-EPES, namespace 3.2.2 | Extensions mal formados; homologación FACe pendiente (#0c) |
| **Modelo 303/347/349/390** | Cálculo y export desde datos reales | Presentación telemática no integrada |
| **Libros IVA** | CSV exportables | Equivalente a gestoría, no sustituto legal sin validación |
| **IRPF 111/190** | Desde contabilidad + orientativo desde nómina | Nómina no homologada |
| **VIES / intracomunitario** | SOAP real + persistencia | Operativo |
| **Recargo equivalencia** | Cálculo y mapeo 303 | Operativo con datos Billing |
| **SEPA** | Generación pain.001/008 + validador | Homologación entidad bancaria (#0d) |
| **Nóminas / TGSS** | RED/TC1/TC2 orientativos | No presentable oficialmente |
| **Crea y Crece (B2B e-factura)** | Emisión FacturaE parcial | Recepción y FACe pendientes |

Comparado con Holded/Sage/Odoo España: el **núcleo fiscal de facturación emitida** está
más avanzado que muchos MVPs, pero les falta el **sello de homologación** y la
**cobertura de nómina/presentación AEAT** que esos productos venden como paquete cerrado.

### Recomendaciones priorizadas

#### Crítico (bloquea producción o cumplimiento legal)

1. **Homologación VeriFactu/SII en entorno PRE AEAT** con certificado FNMT real —
   validar envíos end-to-end antes de clientes en producción (#0b).
2. **Corregir endpoint `POST /api/expenses/{id}/reject`** o quitar el botón del frontend
   — hoy el flujo falla en runtime.
3. **Definir modo VeriFactu por defecto** (`LocalOnly` vs envío TIKE) y checklist legal
   para clientes según fecha obligatoriedad del cliente.
4. **Homologación FacturaE/FACe** o partnership con proveedor certificado (#0c) antes de
   vender Crea y Crece B2B.
5. **Secretos y super-admin:** sustituir email hardcodeado por rol/claim; revisar
   fallbacks MinIO en producción.

#### Importante (funcionalidad core ERP incompleta)

6. **Nóminas Fase 3+:** vacaciones, bajas, finiquitos, o integración con silicio de
   nómina externo (API) si TGSS homologación no es viable corto plazo.
7. **Presentación telemática modelos AEAT** (al menos 303/349) desde la app con
   certificado — diferenciador gestorías.
8. **Conversión automática** `SupplierInvoiceUpload` → `SupplierInvoice` Purchasing (OCR
   + matching).
9. **Open Banking producción:** contrato GoCardless/Nordigen + flujo consentimiento PSD2
   (#28 bloqueo externo documentado).
10. **Almacenamiento documentos S3** en producción (#42d) — biblioteca ya existe con
    MinIO.
11. **Despliegue servidor:** retomar ADR-0003/0020 (TLS, backups offsite, health checks)
    cuando haya VPS.
12. **Ampliar RLS** a tablas críticas restantes (Invoices, Quotes, etc.) como segunda
    barrera.

#### Mejora (calidad, UX, escalabilidad)

13. **Registrar FluentValidation** en todos los módulos que añadan validators.
14. **Unificar estados Sales** UI/backend (`PartiallyDelivered` vs `Shipped`).
15. **Cobertura Accounting.Application** por encima del 18% — módulo de mayor riesgo
    fiscal.
16. **Corregir bloque Extensions FacturaE** (ADR-0013).
17. **CRM comercial:** activity log en pedidos Sales, pipeline visual.
18. **Retirar o actualizar `k8s/deployment.yaml`** para evitar confusión operativa.

### Conclusión del análisis

ErpProject no es un prototipo: es un **monolito modular bien ejecutado**, con
documentación ADR de nivel profesional, integraciones reales entre módulos, y un backlog
de deuda técnica que ADR-0018 marca en ~92% cerrado en código. Para una startup o
gestoría tech-savvy que acepte exports orientativos y homologación pendiente, el
producto **ya sirve para operar** facturación, contabilidad básica, compras/ventas,
tesorería y gastos con OCR.

Para **competir de tú a tú con Holded, Sage, A3 o Odoo España**, el gap no está tanto
en "falta CRUD" como en tres ejes: **(1) sello legal/homologación** AEAT-TGSS-bancos,
**(2) nómina y RRHH completos**, y **(3) operación SaaS en producción** (deploy, S3,
monitorización, soporte). El roadmap ADR-0019 muestra que las piezas de producto
(#38–#42f) están mayormente implementadas; el siguiente salto es menos de arquitectura y
más de **certificación, nómina y go-to-market**.

La honestidad del propio proyecto — disclaimers `MarkAsNonOfficial`, payroll "Fase 2",
`FiscalHomologationController` documentando bloqueos — es una fortaleza: sabes exactamente
qué no vender como "ya cumple la ley" hasta completar los ítems 🔒 de ADR-0018
(#0b–#0d).

## Relación con otros módulos

Este análisis es **transversal** a todos los módulos documentados en ADR-0004–0017 y a
las piezas de plataforma en ADR-0014–0017. No sustituye los ADR por módulo: los complementa
con una vista agregada de madurez y priorización.

Dependencias de lectura recomendadas antes de actuar sobre cualquier brecha:

- Brechas fiscales → [ADR-0013](0013-fiscal-sii-verifactu.md) + ítems #0b–#0d de
  [ADR-0018](0018-calidad-arquitectura.md).
- Brechas de producto → [ADR-0019](0019-producto-roadmap.md).
- Brechas de infra → [ADR-0003](0003-despliegue-infraestructura.md) +
  [ADR-0020](0020-docker-local-produccion.md).
- Brechas de arquitectura → [ADR-0001](0001-arquitectura-general.md) +
  [ADR-0018](0018-calidad-arquitectura.md).

## Evaluación de calidad arquitectónica

> Metodología completa en [ADR-0018](0018-calidad-arquitectura.md). Aquí se resume el
> estado global verificado en julio 2026.

- **SOLID**: SRP mayormente respetado en handlers; OCP aplicado vía eventos/outbox.
  ISP débil en `I*DbContext` anchos (Accounting 29 DbSets). DIP correcto en Application
  (interfaces en core/módulo Application, implementaciones en Infrastructure). Riesgo LSP
  bajo.
- **Clean Architecture**: referencias `.csproj` fluyen hacia adentro en los 9 módulos.
  Violación core→módulos corregida con test `DependencyDirectionArchitectureTests` — no
  reintroducir.
- **Estructura de carpetas**: 4 `.csproj` por módulo con frontera de compilador real;
  `IErpModule` para autoregistro.
- **Duplicación**: catálogo mock ADR-0018 cerrado; cálculos fiscales unificados (VIES,
  VAT, Prorrata corregidos). Persisten heurísticas simplificadas en EFE/patrimonio.
- **Controllers delgados**: 0/71 controllers con lógica inline según ADR-0018; mantener
  al añadir endpoints.
- **Escalabilidad**: listados paginados; sin N+1 documentado en flujos críticos; URLs
  fiscales en `IConfiguration`.
- **CQRS**: flujos de negocio vía `IMediator`; FluentValidation solo registrado en CRM
  — gap a cerrar al añadir validators en otros módulos.
- **Cobertura de test en CI**: ~694 unit + ~164 integración + 7 arquitectura + ~142
  Vitest + 25 Playwright; gates merged ~49%. Accounting.Application umbral 18% — riesgo
  fiscal relativo.

**Madurez dual (jul 2026):**

| Dimensión | % aprox. | Qué mide |
|-----------|----------|----------|
| Código / arquitectura | ~92% | CQRS, tests CI, frontend conectado, Zod, disclaimers UI |
| Comercial / GTM / fiscal homologado | ~55–60% | SII/VeriFactu prod 🔒, SILTRA 🔒, SEPA 🔒 |
| Global ponderado honesto | ~58% | Promedio ponderado incluyendo bloqueos externos |

## Buenas prácticas aplicables

Al usar este ADR para planificar trabajo:

1. **No vender como "cumplimiento garantizado"** lo marcado 🔒 en ADR-0018 (#0b–#0d) ni
   exports `MarkAsNonOfficial`.
2. **Priorizar brechas Críticas** (homologación, reject gastos, super-admin) antes de
   nuevas features de módulo.
3. **Actualizar el ADR del módulo afectado** cuando se cierre una brecha listada aquí.
4. **Re-ejecutar este análisis** tras hitos mayores (homologación AEAT, deploy prod,
   nómina Fase 3) — este documento es snapshot julio 2026.
5. **Ejecutar `DependencyDirectionArchitectureTests`** antes de cualquier cambio en core
   que toque módulos.

## Consecuencias

- El producto **puede operarse** en entornos controlados (gestorías tech-savvy, demos,
  piloto con disclaimers) con la madurez de código actual.
- **No debe comercializarse** como ERP fiscalmente homologado hasta cerrar #0b–#0d y
  definir modo VeriFactu por defecto.
- El gap competitivo principal no es arquitectura sino **certificación legal, nómina/RRHH
  y operación SaaS en producción**.
- Las 18 recomendaciones priorizadas de este ADR deben alinearse con el backlog de
  ADR-0018 y el roadmap de ADR-0019; evitar duplicar ítems ya cerrados (✅).
- Mantener disclaimers honestos (`MarkAsNonOfficial`, payroll "Fase 2",
  `FiscalHomologationController`) como política de producto hasta homologación real.

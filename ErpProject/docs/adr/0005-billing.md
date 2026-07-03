# ADR-0005: Billing (Facturación)

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Billing (`backend/Modules/Billing`) es el módulo que emite los documentos
con validez legal-fiscal del ERP: presupuestos (`Quote`) y facturas
(`Invoice`), conforme al RD 1619/2012 (reglamento de facturación), la Ley
11/2021 de medidas de prevención del fraude fiscal, la Ley 18/2022 Crea y
Crece (FacturaE para B2B) y el RD 1007/2023 (Veri*Factu / sistemas
informáticos de facturación). Es distinto de Sales (ADR-0011), que gestiona
el flujo operativo pedido→entrega y tiene su propia entidad `CustomerInvoice`
sin ninguna de las obligaciones fiscales descritas aquí (ver ADR-0011 para el
detalle de esa diferencia).

Sigue la estructura estándar de módulo de ADR-0001, con `BillingDbContext`
(schema `billing`) como único punto de acceso a `Invoice`, `InvoiceLine`,
`Quote`, `QuoteLine`, `QuoteStatusHistory` y `QuoteNumberSeries`.

## Decisión

### Backend
**Presupuestos (`Quote`)**: numeración `{SeriesPrefix}-{FiscalYear}-{Sequence:D5}`
(p. ej. `PRE-2026-00001`) gestionada de forma aislada por tenant vía
`QuoteNumberSeries`. Ciclo de vida controlado por `Status` (Draft, Sent,
Accepted, Rejected, Expired, Converted, Superseded) y auditado línea a línea
en `QuoteStatusHistory` (incluye `Metadata` JSONB con IP/User-Agent cuando la
transición viene del portal público). Cada `Quote` tiene un
`AcceptanceToken` único (GUID) que habilita el portal público sin login
(`PublicQuotesController`, `/api/v1/public/quotes/{token}`) para
consultar/aceptar/rechazar. Soporta versionado (`Version`, `ParentQuoteId`) y
snapshot de cliente (`ClientType`: Registered/Lead/Manual +
`ClientName/TaxId/Email/...` copiados en el momento).

**Facturas (`Invoice`)**, con `InvoicesController`
(`Api/Controllers/InvoicesController.cs`) — solo `IMediator` — y `CreateInvoiceHandler`/
`LockInvoiceHandler` (`BillingHandlers.cs`) como piezas centrales. El límite de plan
(`IPlanLimitService`) se comprueba en `CreateInvoiceHandler` (`PlanLimitExceededException` → HTTP 402).

**Corregido (paginación, backlog #8):** `GET /api/invoices` y
`GET /api/quotes` devuelven `Paginated*Result` (`{ items, totalCount, page,
pageSize }`) con header `X-Total-Count`; parámetros `page` (default 1) y
`pageSize` (default 50, máx. 500). Frontends de facturación actualizados con
`parseListResponse`.
- **Numeración correlativa**: `{Series}-{FiscalYear}-{Sequence:D6}` (p. ej.
  `A-2026-000001`), calculada dentro de una transacción con
  `pg_advisory_xact_lock` sobre `(companyId, series, fiscalYear)` para
  serializar la generación de secuencia entre requests concurrentes.
- **Snapshot fiscal inmutable**: al crear la factura se copian los datos del
  emisor (`CompanyNif/Name/Address`, desde `Companies`) y del destinatario
  (`ClientNif/Name/Email/Address`, desde `IClientInfoService` si es cliente
  registrado, o de los campos manuales si es `ClientType = "Manual"` — venta
  B2C esporádica). Cambios posteriores en CRM no alteran facturas ya
  emitidas.
- **Hash chain SHA256** (Ley 11/2021 Antifraude): calculado en el momento de
  **crear** la factura (no al bloquear), en `CreateInvoiceHandler`:
  `Hash = SHA256(Number|Total:F2|IssueDate:yyyy-MM-dd|PreviousHash ??
  "GENESIS")`, encadenando cada factura con el `Hash` de la anterior de la
  misma serie/ejercicio (`InvoiceHashService.ComputeHash`, también reutilizado
  por `VerifyHashChainHandler` para auditar la cadena vía
  `GET /api/invoices/verify-chain`).
- **Bloqueo tras contabilización**: `LockInvoiceCommand` /
  `LockInvoiceHandler` marca `IsLocked = true`, `Status = "Locked"`,
  `LockedAt`. En el mismo handler se calcula además la "huella" Veri*Factu
  (`VerifactuHuella`/`VerifactuQrUrl`, RD 1007/2023, Anexo II, encadenada por
  separado a la huella anterior), se encola el envío asíncrono a la AEAT vía
  Hangfire (`IVerifactuSubmissionGateway.EnqueueVerifactuSubmission` →
  `VerifactuSubmissionJob`), se escribe un `AuditLog` inmutable con su propio
  hash, y finalmente se publica `InvoiceApprovedEvent` (MediatR) para que
  Accounting genere el asiento contable. Intentar bloquear una factura ya
  bloqueada lanza `InvalidOperationException`.
- **Veri*Factu — anulación y auditoría** (ADR-0018 #0a ✅): `POST
  /api/invoices/{id}/verifactu/anular`; `VerifactuSubmissionLog`;
  `GET /api/invoices/{id}/verifactu/submissions`; leyenda PDF.
- **Modo no-VERI*FACTU**: `Verifactu:SubmissionMode=LocalOnly` — registro local
  sin remisión TIKE (`VerifactuRealtimeSubmission=false` en factura).
- **IVA español**: `InvoiceLine.TaxRate` (21/10/4/0-Exento), con
  `TipoOperacion` ("Nacional" / "IntraComunitario" art. 25 LIVA /
  "Exportacion" art. 21 LIVA) para casillas del Modelo 303. Si la línea es
  intracomunitaria y se solicita `ValidateEuVatWithVies`, se valida el NIF-IVA
  contra el servicio VIES antes de emitir.
- **IRPF**: `Invoice.IrpfRate`/`IrpfAmount` (retención a profesionales,
  típicamente 15%, 0% si no aplica).
- **Recargo de equivalencia**: `InvoiceLine.SurchargeRate`/`SurchargeAmount`
  (5.2%/1.4%/0.5%), contabilizado en la cuenta 4770 al postear (con fallback
  a 477 si esa cuenta no existe en el plan contable de la empresa).
- **Facturas rectificativas**: `InvoiceType = "Rectificativa"` +
  `RectifiedInvoiceId` (FK a la factura original) +
  `RectificationReasonCode` (obligatorio, letras A–I según Art. 15.1 RD
  1619/2012) + `RectificationReasonText`/periodo opcional. Se generan por dos
  vías: dentro de `CreateInvoiceHandler` (validación explícita del código de
  causa) o mediante `CreateCreditNoteCommand`/`CreateCreditNoteHandler`, que
  clona automáticamente las líneas de la factura original en negativo bajo
  la serie `"R"`.
- **Cobro**: `POST /api/invoices/{id}/pay` → `MarkPaidCommand` marca
  `Status = "Paid"` (idempotente si ya estaba pagada) y publica
  `PaymentReceivedEvent` — consumido por Accounting (asiento 572/430) y por
  CRM (`PaymentReceivedActivityHandler`, ver ADR-0004). También hay un
  `PaymentReceivedOutboxHandler` en Billing que releva el mismo evento a la
  tabla `Outbox` para consumidores externos.
- **FacturaE 3.2.2**: `FacturaEController`
  (`/api/v1/billing/facturae/{invoiceId}`) despacha `GenerateFacturaEQuery` →
  `IFacturaEService` genera el XML bajo demanda (requiere factura bloqueada).
  Es un servicio sin estado: genera el XML a partir del `Invoice` en cada llamada.
  **Nota (ADR-0018 #0c):** `GET .../signed` (XAdES si cert SII); `GET .../validate`
  (`FacturaEXmlStructureValidator` offline); `POST .../submit-face` (SOAP validado
  con `FaceSoapStructureValidator` + HTTP opcional `Face:SendEnabled`); `.xml` sin
  firmar en descarga estándar.
- **PDF**: `GET /api/invoices/{id}/pdf` (`IInvoicePdfService`) y envío por
  email (`POST /api/invoices/{id}/send`), ambos exigen `IsLocked`.

**Nota sobre entidades no conectadas**: `Domain/Entities/FacturaE.cs`
(`FacturaEDocument`, `VerifactuDeclaration`, `FacturaEGraphic`) y
`Domain/Entities/InvoiceValidation.cs` (`SimplifiedInvoice`,
`CreditNoteValidation`, `InvoiceSequecing`) definen entidades que **no**
tienen `DbSet` en `BillingDbContext` ni se referencian desde ningún
handler/controller — son scaffolding sin usar. La generación de FacturaE
real es sin estado (no persiste `FacturaEDocument`); no asumir que estas
clases están activas.

### Frontend
`frontend/src/app/billing/`:
- `page.tsx` — listado de facturas, formulario de creación (cliente
  Registered/Manual, líneas, tipo de factura, rectificativa con
  `rectifiedInvoiceId` + `rectificationReasonCode`), botón de verificación
  de cadena de hashes (`showChainModal`).
- `[id]/page.tsx` — detalle de factura.
- `credit-notes/page.tsx` — gestión de facturas rectificativas/abonos.
- `facturae/page.tsx` — descarga de XML FacturaE.
- `quotes/page.tsx` y `quotes/[id]/page.tsx` — gestión de presupuestos,
  incluyendo estados, líneas con descuento por línea/global y conversión a
  factura.

### Modelo de datos
Esquema `billing`. Migraciones relevantes (orden cronológico):
1. `InitialCreate`
2. `AddFullQuoteModule` — módulo completo de presupuestos.
3. `AddManualClientToInvoice` — soporte de `ClientType = "Manual"` (B2C).
4. `AddTipoOperacionToInvoiceLine` / `AddInvoiceLineTipoOperacion` —
   clasificación de operación IVA para Modelo 303.
5. `AddInvoiceFiscalSnapshot` — columnas de snapshot emisor/destinatario.
6. `Phase0InvoiceCompliance` — campos de cumplimiento adicionales
   (Veri*Factu, VIES, rectificativas).

Índices únicos: `(CompanyId, Number)` en `Invoice`, `(CompanyId, Number)` en
`Quote`, `AcceptanceToken` único en `Quote`. `Quote.TaxBreakdown` es
`jsonb`. Referencias cruzadas a otros módulos (`Quote.ClientId` → CRM,
`QuoteLine.ProductId` → Inventory) son "soft references" — columnas sin FK
de EF, resueltas en la capa de aplicación, tal como se documenta
explícitamente en los comentarios de `BillingDbContext`.

### Flujo end-to-end representativo
Creación y bloqueo de una factura, con propagación a Accounting:
1. `POST /api/invoices` → `CreateInvoiceHandler`: adquiere el advisory lock,
   calcula el siguiente `SequenceNumber`, copia el snapshot fiscal, valida
   VIES si aplica, calcula el hash SHA256 encadenado al anterior de la serie,
   persiste `Invoice` + `InvoiceLine[]` en `Status = "Draft"`.
2. `POST /api/invoices/{id}/lock` → `LockInvoiceHandler`: marca
   `IsLocked/LockedAt`, calcula la huella Veri*Factu, encola el envío AEAT
   (Hangfire), escribe `AuditLog`, y publica `InvoiceApprovedEvent`.
3. `InvoiceApprovedEventHandler` (**Accounting**, ADR-0006) recibe el evento,
   comprueba idempotencia (`JournalEntries.AnyAsync(SourceType=="Invoice" &&
   SourceId==...)`) y genera el asiento: Debe 430 (Clientes) por `Total`;
   Haber 700 (Ventas) por `Subtotal`; Haber 477 (IVA Repercutido) si
   `TaxAmount > 0`; Debe 4751 (Retenciones) si `IrpfAmount > 0`; Haber 4770
   (Recargo equivalencia) si `SurchargeAmount > 0`. Verifica que Debe = Haber
   antes de guardar.
4. Cuando el cliente paga, `POST /api/invoices/{id}/pay` publica
   `PaymentReceivedEvent`, consumido por Accounting (asiento de cobro
   572/430) y por CRM (`PaymentReceivedActivityHandler`).

## Relación con otros módulos
- **Accounting** (ADR-0006): consumidor directo de `InvoiceApprovedEvent` y
  `PaymentReceivedEvent` — Billing nunca escribe asientos contables
  directamente, solo publica eventos.
- **CRM** (ADR-0004): Billing lee datos de cliente vía `IClientInfoService`
  (sin dependencia directa de `ICrmDbContext`) al crear facturas; CRM
  consume `QuoteAcceptedEvent` (conversión de Lead a Client) y
  `PaymentReceivedEvent` (timeline de actividad) publicados por Billing.
- **Fiscal/SII/Veri*Factu** (ADR-0013): la huella y el envío a AEAT se
  calculan en `LockInvoiceHandler`, pero el envío real ocurre en un job
  Hangfire (`VerifactuSubmissionJob`) fuera del ciclo de request.
- **API pública** (ADR-0016): `PublicInvoicesController`
  (`/api/v1/public/invoices`) delega en `GetPublicInvoicesQuery` /
  `GetPublicInvoiceByIdQuery` (lectura vía `X-Api-Key`), y `PublicQuotesController`
  expone el portal de aceptación de presupuestos sin autenticación (por `AcceptanceToken`).
- **Inventory**: `InvoiceApprovedEvent.Lines` incluye `ProductId`/
  `Quantity`/`UnitPrice` pensado para integración de stock (el DTO existe en
  `DomainEvents.cs` con comentario explícito "Inventory integration"),
  aunque el consumo de ese campo debe verificarse en el propio módulo
  Inventory (ADR-0008), no en Billing.

## Buenas prácticas aplicables
- Nunca mutar una factura con `IsLocked = true` fuera de los flujos ya
  existentes (rectificativa/abono) — es la garantía legal de
  no-alterabilidad de la Ley Antifraude.
- El hash SHA256 se calcula al **crear**, no al bloquear; la huella
  Veri*Factu se calcula al **bloquear**. No confundir ambos mecanismos: son
  cadenas independientes con propósitos distintos (antifraude interno vs.
  interoperabilidad con AEAT).
- Cualquier cambio en el cálculo de IVA/IRPF/recargo debe mantener
  cuadrado el asiento contable generado por
  `InvoiceApprovedEventHandler` (Debe == Haber), que lanza excepción si no
  cuadra.
- Antes de reutilizar `FacturaEDocument`/`SimplifiedInvoice`/
  `CreditNoteValidation`/etc., comprobar si siguen sin `DbSet` — si se
  decide activarlas, requiere migración nueva y registro en
  `BillingDbContext`.
- Los códigos de causa de rectificación deben restringirse al conjunto
  A–I validado en `CreateInvoiceHandler` (`RectificacionCodigosValidos`).

## Consecuencias
- La doble cadena de hashes (antifraude + Veri*Factu) añade complejidad de
  auditoría pero permite verificar la integridad histórica desde dos
  ángulos: `GET /api/invoices/verify-chain` (interno) y la huella AEAT
  (externo).
- El uso de "soft references" (sin FK de EF) hacia CRM e Inventory simplifica
  el despliegue independiente de módulos, pero traslada la responsabilidad
  de integridad referencial a la capa de aplicación.
- Existen clases de dominio (FacturaE persistente, SimplifiedInvoice,
  InvoiceSequecing) que sugieren una migración incompleta hacia un modelo
  más granular; cualquier trabajo futuro en esa área debe empezar
  comprobando si siguen desconectadas del `DbContext`.
- `frontend/src/app/billing/facturae/page.tsx` — ✅ Corregido (backlog #17):
  lista facturas bloqueadas desde `/api/proxy/invoices`, descarga XML FacturaE
  (`/api/proxy/v1/billing/facturae/{id}`) y PDF (`/api/proxy/invoices/{id}/pdf`);
  enlace a `/verifactu` para envío VERI*FACTU por período.

# ADR-0013: Fiscal — SII y VeriFactu

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Esta ADR documenta el cumplimiento de dos obligaciones fiscales españolas
transversales, que no viven en un módulo `backend/Modules/` propio sino
repartidas entre el core (`Erp.Api`, `Erp.Application`, `Erp.Domain`,
`Erp.Infrastructure`) y el módulo Billing:

- **SII (Suministro Inmediato de Información):** envío a la AEAT de los
  libros registro de facturas emitidas y recibidas mediante XML firmado.
- **VeriFactu (RD 1007/2023):** cadena de huellas (hash SHA-256) por factura
  y envío del registro de facturación al sistema TIKE de la AEAT, como parte
  del régimen de "sistemas informáticos de facturación" (Ley 11/2021
  antifraude).

Solo se describe lo que existe realmente implementado en el código; no se
asume alcance normativo más allá de lo que el código cubre.

## Decisión
### Backend
**Controladores API (`backend/Erp.Api/Controllers/`):**
- `SiiController` (`api/sii`, `[Authorize]`) — inyecta `SiiXmlGenerator`,
  `SiiSigningService`, `SiiSubmissionService`, `VerifactuXmlGenerator` y
  `VerifactuSubmissionService` (todos de `Erp.Infrastructure.Services.Sii`).
  Endpoints: `GET emitidas`/`GET recibidas` (XML SII descargable),
  `GET preview` (Base64 de ambos XML para revisión), `POST submit` (firma
  XAdES-BES con `SiiSigningService` y envía con `SiiSubmissionService`;
  requiere `Sii:CertPath`/`Sii:CertPass` configurados — si no, devuelve 400),
  `GET verifactu` (XML de registro VeriFactu vía
  `VerifactuXmlGenerator.GenerateRegistroAsync`) y
  `POST verifactu/submit` (envío al TIKE vía `VerifactuSubmissionService`).
- `TaxController` (`api/tax`) — validación de NIF-IVA intracomunitario contra
  el servicio oficial VIES de la UE, vía `IViesService`
  (`POST`/`GET api/tax/vies/validate`). Comparte la misma fuente SOAP y el
  texto `Advice` (`ViesResponseMapper`) que `ViesController` del módulo
  Accounting (`api/v1/accounting/vies/validate`, que además persiste en
  `IntraEuOperations` vía `ValidateViesCommand`).
- `FiscalCalendarController` (`api/fiscal/calendar`) — calendario de
  obligaciones tributarias españolas por empresa: `GET` (lista/año),
  `GET {id}`, `POST generate/{year}` (genera automáticamente eventos vía
  `IFiscalCalendarService.GenerateYearCalendarAsync`, idempotente por
  modelo+periodo), `PATCH {id}/submit` (marca como presentado) y `GET
  overdue` (vencidos). Entidad `FiscalEvent`
  (`Erp.Domain/Entities/Core/FiscalEvent.cs`) con `ModelCode`, `ModelName`,
  `Year`/`Quarter`/`Month`, `DeadlineDate`, `Status`, `SubmissionReference`.

**Servicios de infraestructura (`backend/Erp.Infrastructure/Services/`):**
- `VerifactuService.cs` (`Erp.Infrastructure.Services`) implementa
  `IVerifactuService`: calcula la `Huella` como
  `SHA256(NifEmisor & NumSerie & Fecha & TipoFactura & CuotaTotal &
  ImporteTotal & HuellaAnterior & NifSoftware & IdSistema & NumRegistro &
  FechaHora)` (orden de campos según RD 1007/2023 Anexo II), genera la URL de
  validación QR de la AEAT (`ValidarQR`) y el propio SVG del QR (librería
  `QRCoder`). Los identificadores del software (`NifSoftware`,
  `NombreSoftware`, `IdSistema`) se configuran en `Verifactu:*`
  (`appsettings.json`).
- `Services/Sii/VerifactuXmlGenerator.cs` y
  `Services/Sii/VerifactuSubmissionService.cs` — generan el XML de registro
  VeriFactu y lo envían por HTTP POST (no SOAP) a los endpoints TIKE de la
  AEAT: PRE `https://prewww1.aeat.es/wlpl/TIKE-CONT/ws/SuministroFacturas` y
  PROD el equivalente en `agenciatributaria.gob.es`. La autenticación es
  mTLS con certificado digital FNMT, mismo `HttpClient` nombrado `sii-aeat`
  que usa SII. Se parsea `EstadoEnvio` de la respuesta AEAT
  (`Correcto`/`AceptadoConErrores`/`Incorrecto`) para determinar éxito.
- `VerifactuOptions.cs` — opciones tipadas (`Verifactu:*`), incluye
  `UseProduction` para alternar PRE/PROD.

**Origen del hash chain y disparo del envío (módulo Billing):**
- `Invoice` (`backend/Modules/Billing/Domain/Entities/Invoice.cs`) tiene los
  campos `Hash`/`PreviousHash` (cadena SHA-256 interna, ley antifraude
  genérica) y por separado `VerifactuHuella`, `VerifactuQrUrl`,
  `VerifactuSubmittedAt` (específicos de RD 1007/2023).
- `LockInvoiceHandler` (`Modules/Billing/Application/Features/Billing/Handlers/BillingHandlers.cs`)
  es el punto donde, al bloquear una factura (`IsLocked=true`), se calcula la
  huella: busca la `VerifactuHuella` de la factura anterior de la misma
  serie/ejercicio (`SequenceNumber < actual`), determina `TipoFactura`
  (`F1` normal, `R1` rectificativa, `F2` simplificada) y llama a
  `IVerifactuService.Compute(...)`. Tras guardar, si se generó huella,
  encola el envío asíncrono con `IVerifactuSubmissionGateway.EnqueueVerifactuSubmission(invoiceId)`.
- `VerifactuSubmissionGateway`
  (`Modules/Billing/Infrastructure/Services/VerifactuSubmissionGateway.cs`)
  llama a `VerifactuSubmissionJob.Enqueue(invoiceId)` — un job de Hangfire
  con reintentos automáticos (`[AutomaticRetry(Attempts = 3, DelaysInSeconds
  = {10, 30, 120})]`). El job regenera el XML del periodo y llama a
  `IVerifactuSubmissionService.SubmitSingleAsync`; si la AEAT acepta, marca
  `inv.VerifactuSubmittedAt = DateTime.UtcNow`. El bloqueo de la factura
  **no** depende del resultado del envío — este es asíncrono y su log es la
  fuente de verdad en caso de fallo.
- `TaxReport` (`Erp.Domain/Entities/Tax/TaxReport.cs`) es una entidad simple
  (`Period`, `TotalCollected`, `TotalPaid`, `NetTax`) sin controlador ni
  handler visible que la use activamente en el código explorado — parece un
  agregado de reporting fiscal aún no conectado a un flujo completo.

### Frontend
`frontend/src/app/fiscal/page.tsx` — calendario fiscal (`fetch
/api/proxy/fiscal/calendar`, marcar como presentado).
`frontend/src/app/sii/page.tsx` — descarga de XML emitidas/recibidas y envío
(`fetch /api/proxy/sii/submit`).
`frontend/src/app/verifactu/page.tsx` — genera y envía el XML VeriFactu
(`fetch /api/proxy/sii/verifactu` y `/api/proxy/sii/verifactu/submit`), sobre
el mismo controlador `SiiController`.

### Modelo de datos
No hay un DbContext propio: `FiscalEvent` se persiste vía
`IApplicationDbContext` (core), y los campos VeriFactu/hash chain viven como
columnas del propio `Invoice` en `BillingDbContext` (migraciones
`AddVerifactuToInvoice`, `AddVerifactuSubmittedAt` en
`Erp.Infrastructure/Migrations/`, y en `Modules/Billing/Infrastructure/Migrations/`
las de `InvoiceCompliance`). No existe una tabla separada de "registros
VeriFactu enviados"; el estado de envío se rastrea con un único timestamp
nullable (`VerifactuSubmittedAt`) por factura.

### Flujo end-to-end representativo
Emisión y bloqueo de una factura → cadena de huellas VeriFactu → envío AEAT:
1. El usuario bloquea una factura (`LockInvoiceCommand` →
   `LockInvoiceHandler`).
2. El handler recupera la `VerifactuHuella` de la factura anterior de la
   misma serie y ejercicio fiscal (encadenamiento requerido por el
   reglamento).
3. `VerifactuService.Compute(...)` calcula la nueva huella SHA-256 y la URL
   de validación QR; ambas se persisten en la factura junto con
   `LockedAt`/`IsLocked=true`.
4. Si se generó huella, se encola `VerifactuSubmissionJob` vía Hangfire
   (`VerifactuSubmissionGateway.EnqueueVerifactuSubmission`).
5. El job (con hasta 3 reintentos con backoff) regenera el XML del periodo
   mensual de la factura y lo envía por HTTPS/mTLS al endpoint TIKE de la
   AEAT (PRE o PROD según `Verifactu:UseProduction`).
6. Si la AEAT responde `EstadoEnvio=Correcto`, se marca
   `VerifactuSubmittedAt`; en caso contrario queda registrado en el log y la
   factura permanece bloqueada igualmente (el envío no bloquea el flujo de
   negocio).
7. El usuario puede consultar/reenviar manualmente el mismo XML desde
   `frontend/src/app/verifactu/page.tsx` (`GET`/`POST api/sii/verifactu*`),
   o generar y enviar el SII de facturas emitidas/recibidas del mes desde
   `frontend/src/app/sii/page.tsx`.

## Relación con otros módulos
- **Billing (ADR-0005):** es el origen real del hash chain y del disparo de
  envío VeriFactu (`Invoice`, `LockInvoiceHandler`,
  `VerifactuSubmissionJob`); SII/VeriFactu como ADR son, en la práctica, una
  extensión de compliance de Billing implementada en el core.
- **Accounting (ADR-0006):** los modelos AEAT de IVA (303, 390, 347) que
  Accounting genera en `AccountingExportController` son declaraciones
  fiscales relacionadas pero técnicamente independientes del canal SII/TIKE
  descrito aquí — SII reporta facturas individuales en tiempo casi real,
  mientras que los modelos 303/390 son liquidaciones periódicas agregadas.
- **Facturae:** `Modules/Billing/Api/Controllers/FacturaEController.cs`
  (`api/v1/billing/facturae`) genera el formato de factura electrónica
  Facturae (típico B2G); no tiene relación de código con SII ni VeriFactu (no
  se encontraron referencias cruzadas), son mecanismos de cumplimiento
  distintos que conviven en Billing.

## Buenas prácticas aplicables
- Cualquier cambio en el cálculo de la `Huella` debe respetar exactamente el
  orden y formato de campos del Anexo II del RD 1007/2023 (ver comentario en
  `VerifactuService.ComputeHuella`); un cambio de formato invalidaría la
  cadena de huellas ya generada.
- El envío a AEAT (SII y VeriFactu) debe seguir siendo asíncrono
  (Hangfire) y no bloquear el bloqueo/emisión de la factura — el patrón
  actual separa explícitamente "la factura queda bloqueada" de "el envío se
  reintenta en background".
- No activar `Verifactu:UseProduction` sin homologación previa en el entorno
  PRE de la AEAT, como advierte el comentario en
  `VerifactuSubmissionService`.
- Usar siempre `Erp.Api/Controllers/TaxController.cs` para validación VIES
  real; el `ViesController` de Accounting es un stub de datos de ejemplo.

## Consecuencias
- La firma XAdES-BES y el envío SOAP/HTTP a la AEAT dependen de un
  certificado FNMT configurado en `Sii:CertPath`/`Sii:CertPass`; sin él,
  `SiiController.Submit` devuelve 400 explícitamente — en entornos de
  desarrollo/demo estos flujos no son operativos end-to-end contra la AEAT
  real.
- No existe una tabla de auditoría dedicada a los envíos SII/VeriFactu más
  allá del log de aplicación y el timestamp `VerifactuSubmittedAt`; no es
  posible reconstruir un histórico completo de reintentos o respuestas AEAT
  desde la base de datos.
- `TaxReport` (`Erp.Domain/Entities/Tax/TaxReport.cs`) existe como entidad
  pero no se localizó un controlador/handler que la persista activamente —
  posible funcionalidad incompleta o pendiente de conectar; no se debe asumir
  que hay un flujo de reporting fiscal automático detrás de esa entidad sin
  verificarlo de nuevo antes de usarla.

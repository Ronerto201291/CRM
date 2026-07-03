# ADR-0013: Fiscal — SII y VeriFactu

## Estado
Aceptado — refleja la implementación actual del código en `main`. **Actualización crítica**: una auditoría de corrección (no solo de existencia) encontró que **VeriFactu y SII, tal como están hoy, serían rechazados por la AEAT** — no son mock (llaman de verdad a los servicios de la AEAT, firman digitalmente, calculan huellas), pero el resultado no es válido frente a la especificación real. Ver la sección dedicada "Auditoría de corrección frente a especificación externa" más abajo antes de asumir que cualquiera de los dos flujos es funcional en producción.

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
  (`POST`/`GET api/tax/vies/validate`). Es el endpoint VIES **real** del
  sistema (a diferencia del stub en
  `Modules/Accounting/Api/Controllers/ViesController.cs`, ver ADR-0006);
  requiere `countryCode` ISO-2 y `vatNumber`, y devuelve además un campo
  `Advice` orientativo sobre si procede exención por art. 25 LIVA.
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

## Auditoría de corrección frente a especificación externa

Todo lo descrito arriba en "Decisión" existe como código real: llama de
verdad a los endpoints TIKE/SII de la AEAT, firma digitalmente, calcula
huellas SHA-256, genera XML. Esto lo diferencia de los mock puros
documentados en el catálogo de ADR-0018 (que devuelven `Ok(new {...})`
hardcodeado). Pero "es código real" no es lo mismo que "es correcto frente
a la especificación exacta que dice implementar" — y no se había verificado
esto último hasta esta auditoría dedicada. Resultado: **los tres mecanismos
de cumplimiento fiscal automatizado de este ERP (VeriFactu, SII, FacturaE)
fallarían contra los sistemas reales de la AEAT/FACe hoy mismo**, cada uno
por motivos propios y confirmados leyendo el código exacto citado.

### VeriFactu — la huella (hash) no coincidiría con la que recalcula la AEAT
- **Fórmula de la huella incorrecta**: `VerifactuService.ComputeHuella` hashea
  **11 campos** (`VerifactuService.cs:56-69`) en vez de los 8 exactos del
  Anexo II del RD 1007/2023 — añade de más el NIF del software, el
  `IdSistema` y el número de registro. La AEAT recalcula la huella con sus 8
  campos oficiales; al no coincidir, rechaza el registro.
- **Formato de concatenación incorrecto**: los valores se unen en crudo
  (`valor1&valor2&...`, `VerifactuService.cs:56`) en vez del formato
  `clave=valor` (`IDEmisorFactura=...&NumSerieFactura=...`) que exige la
  especificación técnica — otro motivo independiente de que el hash no
  coincida.
- **Inconsistencia F1/F2 entre el hash y el XML**: una factura simplificada
  se hashea como `F2` (`BillingHandlers.cs:376`) pero el XML la declara `F1`
  (`VerifactuXmlGenerator.cs:127`, que solo distingue `Rectificativa`/resto).
- **XML con forma de SII, no de VeriFactu**: elemento raíz
  `SuministroLRFacturasEmitidas` en vez de `RegFactuSistemaFacturacion`;
  faltan `TipoHuella` e `IDVersion`; el encadenamiento nunca declara
  `PrimerRegistro="S"` para la primera factura de una serie
  (`VerifactuXmlGenerator.cs:96-185`).
- **`AceptadoConErrores` tratado como fallo total**: `VerifactuSubmissionService.cs`
  hace `ok = estado == "Correcto"`, así que un registro que la AEAT sí aceptó
  (con avisos) se reintenta indefinidamente sin dejar rastro de que fue
  aceptado.
- **Reenvíos duplicados**: el job de envío reprocesa todas las facturas sin
  marcar del mes cada vez que se bloquea una factura nueva, pudiendo
  reenviar a la AEAT facturas ya aceptadas.
- **Sin registro de anulación**: no existe ningún camino de cumplimiento para
  anular un registro ya enviado (solo facturas rectificativas nuevas).
- **Sin tabla de auditoría/eventos**: los envíos fallidos o con errores no
  dejan rastro consultable en base de datos, solo en logs de aplicación.
- **Falta la leyenda legal obligatoria** ("VERI*FACTU" o la alternativa de
  modo local) en el PDF de factura — sí lleva QR y huella, pero no el texto
  exigido.
- **No existe el modo "no VERI*FACTU"** (registro local sin envío en tiempo
  real): el sistema solo implementa el modo de envío inmediato
  (`VerifactuXmlGenerator.cs:182`, `TipoUsoPosibleSoloVerifactu = "S"`).
- Lo que sí está bien: el encadenamiento recupera correctamente la huella
  anterior de la misma serie/ejercicio fiscal (`BillingHandlers.cs:364-371`);
  el guard de arranque bloquea NIFs de software de ejemplo en producción
  (`VerifactuOptions.AssertValid`, `VerifactuOptions.cs:59-80`); el patrón
  async con reintentos de Hangfire es sólido; el PRE/PROD por defecto es
  seguro (`UseProduction=false`); el frontend está de verdad conectado (no
  es mock).

### SII — tres bugs independientes garantizan rechazo
- **Namespace único mal aplicado**: `Cabecera`/`Titular` se serializan bajo
  un único namespace (`SiiModels.cs:10,20`) cuando el XSD real de SII exige
  dos namespaces distintos para la envoltura `SuministroLR` y el contenido
  común `SuministroInformacion`.
- **Sobre SOAP con el documento anidado dos veces**: `signedXml` ya es un
  documento `<SuministroLRFacturasEmitidas>` completo, y
  `BuildSoapEnvelope` (`SiiSubmissionService.cs:86-98`) lo vuelve a envolver
  dentro de otro `<sii:SuministroLRFacturasEmitidas>` — XML mal formado.
  Además el firmante no omite la declaración `<?xml?>`
  (`SiiSigningService.cs:117`), que queda incrustada a mitad del sobre.
- **Falta `Contraparte` en facturas emitidas**: confirmado que
  `SiiXmlGenerator.cs:60-92` (bloque `FacturaExpedida`) nunca rellena
  `Contraparte` (identificación del cliente), obligatorio para F1. En
  recibidas sí se rellena (`SiiXmlGenerator.cs:153`) pero usando el campo
  `CuotaRepercutida` (IVA repercutido) cuando debe informarse
  `CuotaSoportada` (IVA soportado) — el campo ni siquiera existe en el
  modelo (`SiiModels.cs:130`).
- **Firma XAdES-BES con problema de orden de operaciones**: `SignedProperties`
  se calcula/digiere antes de moverse dentro de la firma
  (`SiiSigningService.cs:68-71,103-108`), lo que cambia el contexto de
  canonicalización inclusive y probablemente invalida la firma; el
  `IssuerSerialV2` tampoco es DER válido, solo texto Base64 de un string
  (`SiiSigningService.cs:159-160`).
- **API de certificado obsoleta**: `new X509Certificate2(path, pass, flags)`
  (`SiiSigningService.cs:55`, `Erp.Infrastructure/DependencyInjection.cs:86`)
  es el patrón `SYSLIB0057` ya señalado en otras partes del código; sin
  comprobación de expiración antes de firmar.
- Lo que sí está bien: SOAP real con mTLS (a diferencia de VeriFactu, que
  hace POST crudo), Polly retry/circuit-breaker, algoritmos RSA-SHA256/C14N
  correctos, fechas `dd-MM-yyyy` correctas, filtro por facturas bloqueadas.

### FacturaE — se presenta como firmado sin estarlo
- **Extensión `.xsig` sin firma real**: `FacturaEService.cs:53` nombra el
  fichero `.xsig` (que implica "firmado") pero no genera ningún
  `ds:Signature` — el documento nunca se firma. El propio comentario del
  controller afirma "El XML generado cumple el esquema oficial"
  (`FacturaEController.cs:30`), lo cual no es cierto para un envío FACe real
  (que exige XAdES).
- **Namespace probablemente incorrecto**:
  `http://www.facturae.gob.es/formato/Version3.2.2/Facturae32.xsd`
  (`FacturaEService.cs:20`) — necesita confirmarse contra el XSD vivo, alta
  probabilidad de ser distinto del namespace real de la versión 3.2.2.
  ver.
- **Bloque `Extensions` mal formado** dentro de `FileHeader`
  (`FacturaEService.cs:236-264`) con una estructura inventada, no la que
  define el estándar.
- **NIF sin validar**: se pasa tal cual (`FacturaEService.cs:308`), sin
  ningún dígito de control — coincide con el hallazgo sistémico de más abajo.
- **Dirección con placeholders hardcodeados**: `PostCode="00000"`,
  `Town="N/D"`, `Province="N/D"` (`FacturaEService.cs:281-283`) en vez de
  leer los datos reales de la empresa/cliente.
- **Sin ningún camino de envío a FACe**: solo genera el archivo descargable,
  ninguna transmisión SOAP/REST.
- Lo que sí está bien: la aritmética de IVA/IRPF por línea y su agregación
  (`FacturaEService.cs:65-71,151,163`) es correcta; el orden general de
  bloques sigue razonablemente la forma del estándar.

### Hallazgo sistémico transversal — sin validación de NIF/CIF/NIE en ningún sitio
Búsqueda en todo el backend: cero clases/funciones que validen el dígito de
control de un NIF/CIF/NIE español (`grep` de patrones de validación: 0
resultados salvo la heurística de primer carácter de `FacturaEService.IsCompanyNif`,
que solo decide persona física/jurídica, no valida nada). Esto afecta a la
vez a CRM (alta de cliente), Billing (emisión de factura), FacturaE, SII y
VeriFactu — un NIF con la letra de control equivocada se acepta sin aviso en
el alta y solo se descubriría, en el mejor de los casos, cuando la AEAT
rechace el envío.

### Cadena de hash genérica antifraude (Ley 11/2021) — correcta, con una duplicación menor
A diferencia de VeriFactu, esta cadena (`Invoice.Hash`/`PreviousHash`,
`InvoiceHashService.ComputeHash`) no se envía a la AEAT para que la
recalcule con una fórmula fija — es un mecanismo de trazabilidad interna. La
fórmula (`InvoiceHashService.cs:15-20`) es razonable y consistente. Único
hallazgo: el cálculo está duplicado — se repite inline en
`BillingHandlers.cs:218-219` en vez de llamar al método estático
`InvoiceHashService.ComputeHash` que ya existe (y que solo se usa hoy para
verificar, no para generar) — riesgo de que las dos copias diverjan si se
cambia la fórmula en un solo sitio.

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

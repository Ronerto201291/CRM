# ADR-0013: Fiscal — SII y VeriFactu

## Estado
Aceptado — refleja la implementación actual del código en `main`. **Actualización (re-verificada tras una ronda de correcciones)**: la auditoría original encontró que VeriFactu y SII serían rechazados por la AEAT tal como estaban. Tras una ronda de correcciones, **se ha vuelto a verificar el código real** (no solo se ha confiado en que "se corrigió"): **VeriFactu y SII están ahora corregidos de verdad** (hash de 8 campos con formato `clave=valor`, XML con la forma real de VeriFactu, namespaces duales de SII, sobre SOAP sin anidar dos veces, `Contraparte`/`CuotaSoportada` correctos, orden de firma XAdES arreglado). **FacturaE sigue con un problema real de namespace** y su nuevo validador de estructura lo valida contra la misma constante equivocada (validación circular que no protege de nada). Ver la sección "Auditoría de corrección frente a especificación externa" para el detalle verificado de cada uno.

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
- `SiiController` (`api/sii`, `[Authorize]`) — controller delgado con
  `IMediator`; handlers en `Erp.Infrastructure.Features.Sii` delegan en
  `SiiXmlGenerator`, `SiiSigningService`, `SiiSubmissionService`,
  `IVerifactuXmlGenerator` y `VerifactuSubmissionService`. Endpoints:
  `GET emitidas`/`GET recibidas`, `GET validate`, `GET preview`,
  `POST submit`, `GET verifactu`, `POST verifactu/submit`.
- `TaxController` (`api/tax`) — controller delgado con `IMediator`; despacha
  `ValidateViesCommand` (Accounting) para VIES (`POST`/`GET
  `api/tax/vies/validate`). Comparte `IViesService` SOAP y `ViesResponseMapper`
  con `ViesController` del módulo Accounting (que además persiste en
  `IntraEuOperations`).
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
- Usar `TaxController` o `ViesController` (Accounting) para validación VIES —
  ambos invocan el mismo `IViesService` SOAP (#5); Accounting además persiste
  en `IntraEuOperations`.

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
- **Fórmula de la huella incorrecta**: ~~`VerifactuService.ComputeHuella` hashea
  **11 campos** (`VerifactuService.cs:56-69`) en vez de los 8 exactos del
  Anexo II del RD 1007/2023 — añade de más el NIF del software, el
  `IdSistema` y el número de registro. La AEAT recalcula la huella con sus 8
  campos oficiales; al no coincidir, rechaza el registro.~~ **✅ Corregido
  (ADR-0018 #0a parcial):** `ComputeHuella` usa los 8 campos Anexo II con
  formato `clave=valor&...`.
- **Formato de concatenación incorrecto**: ~~los valores se unen en crudo
  (`valor1&valor2&...`, `VerifactuService.cs:56`) en vez del formato
  `clave=valor` (`IDEmisorFactura=...&NumSerieFactura=...`) que exige la
  especificación técnica — otro motivo independiente de que el hash no
  coincida.~~ **✅ Corregido** junto con el ítem anterior.
- **Inconsistencia F1/F2 entre el hash y el XML**: ~~una factura simplificada
  se hashea como `F2` (`BillingHandlers.cs:376`) pero el XML la declara `F1`
  (`VerifactuXmlGenerator.cs:127`, que solo distingue `Rectificativa`/resto).~~ **✅ Corregido** — `VerifactuTipoFactura.Resolve` unifica hash y XML.
- **XML con forma de SII, no de VeriFactu**: ~~elemento raíz
  `SuministroLRFacturasEmitidas` en vez de `RegFactuSistemaFacturacion`;
  faltan `TipoHuella` e `IDVersion`; el encadenamiento nunca declara
  `PrimerRegistro="S"` para la primera factura de una serie
  (`VerifactuXmlGenerator.cs:96-185`).~~ **✅ Corregido** — generador reescrito con esquema tikeV1.0.
- **`AceptadoConErrores` tratado como fallo total**: ~~`VerifactuSubmissionService.cs`
  hace `ok = estado == "Correcto"`~~ **✅ Corregido**.
- **Reenvíos duplicados**: ~~el job de envío reprocesa todas las facturas sin
  marcar del mes~~ **✅ Corregido** — `GenerateSingleInvoiceRegistroAsync` envía solo la factura bloqueada.
- **Sin registro de anulación**: ~~no existe ningún camino de cumplimiento para
  anular un registro ya enviado~~ **✅ Corregido** — `GenerateAnulacionRegistroAsync`,
  `AnulVerifactuInvoiceCommand` vía `IVerifactuSubmissionGateway.EnqueueVerifactuAnulacion`,
  `POST /api/invoices/{id}/verifactu/anular`.
- **Sin tabla de auditoría/eventos**: ~~los envíos fallidos o con errores no
  dejan rastro consultable en base de datos~~ **✅ Corregido** — entidad
  `VerifactuSubmissionLog` (Alta/Anulacion, éxito/error, respuesta AEAT);
  `GET /api/invoices/{id}/verifactu/submissions`.
- **Falta la leyenda legal obligatoria** ("VERI*FACTU" o la alternativa de
  modo local) en el PDF de factura — ~~sí lleva QR y huella, pero no el texto
  exigido~~ **✅ Corregido** — `InvoicePdfDocument` imprime `VERI*FACTU` cuando
  hay huella.
- **No existe el modo "no VERI*FACTU"** (registro local sin envío en tiempo
  real): ~~el sistema solo implementa el modo de envío inmediato~~ **✅ Corregido**
  — `Verifactu:SubmissionMode=LocalOnly` desactiva envío TIKE y QR; leyenda PDF
  alternativa; `Invoice.VerifactuRealtimeSubmission` persiste el modo por factura.
- Lo que sí está bien: el encadenamiento recupera correctamente la huella
  anterior de la misma serie/ejercicio fiscal (`BillingHandlers.cs:364-371`);
  el guard de arranque bloquea NIFs de software de ejemplo en producción
  (`VerifactuOptions.AssertValid`, `VerifactuOptions.cs:59-80`); el patrón
  async con reintentos de Hangfire es sólido; el PRE/PROD por defecto es
  seguro (`UseProduction=false`); el frontend está de verdad conectado (no
  es mock).

### SII — tres bugs independientes garantizan rechazo
- **Namespace único mal aplicado**: **🟡 Parcial+ corregido** — `SiiModels.cs` usa
  `SiiNamespaces.LR` e `Info` con `[XmlType]`/`[XmlElement]` por namespace
  (Cabecera en `Info`, registros en `LR`). Pendiente: validación runtime contra AEAT.
- **Sobre SOAP con el documento anidado dos veces**: **✅ Corregido** — `BuildSoapEnvelope` inserta el XML firmado directamente en `soapenv:Body` sin segundo `SuministroLR`.
- **Falta `Contraparte` en facturas emitidas**: **✅ Corregido** — `SiiXmlGenerator` rellena `Contraparte` desde snapshot fiscal del cliente. En recibidas: **✅ Corregido** — `CuotaSoportada` en lugar de `CuotaRepercutida`.
- **Firma XAdES-BES con problema de orden de operaciones**: ~~`SignedProperties`
  se calcula/digiere antes de moverse dentro de la firma~~ **🟡 Parcial+ corregido**
  — `SiiSigningService` añade `DataObject` con `SignedProperties` antes de
  `ComputeSignature`; `SigningCertificate` v1 con `IssuerSerial` (X509IssuerName +
  X509SerialNumber) en lugar de `IssuerSerialV2` DER inválido. Pendiente:
  homologación con certificado real.
- **API de certificado obsoleta**: ~~`new X509Certificate2(path, pass, flags)`~~ **✅ Corregido**
  — `Pkcs12CertificateLoader` + `X509CertificateLoader.LoadPkcs12` en `SiiSigningService`
  y handler HTTP mTLS.
- **Homologación sin certificado**: **✅ Corregido** — `GET /api/sii/validate` +
  `SiiXmlStructureValidator` (namespaces, Cabecera, Contraparte/CuotaSoportada).
- Lo que sí está bien: SOAP real con mTLS (a diferencia de VeriFactu, que
  hace POST crudo), Polly retry/circuit-breaker, algoritmos RSA-SHA256/C14N
  correctos, fechas `dd-MM-yyyy` correctas, filtro por facturas bloqueadas.

### FacturaE — namespace y validador circular ya corregidos; firma ya real
- **Firma real**: ~~sin `ds:Signature`~~ **✅ Corregido, re-verificado directamente**
  — `GenerateSignedAsync` → `GenerateCoreAsync(sign: true)` llama a
  `_signer.Sign(xmlString)` (`FacturaEService.cs:43-80`), el mismo firmante
  XAdES de SII (ya con el orden de operaciones corregido, ver sección SII). El
  archivo solo se nombra `.xsig` cuando `sign=true` (línea 78); si no, `.xml`.
  El texto anterior de esta sección decía "sigue sin generarse ds:Signature",
  lo cual ya no es cierto tras la última ronda de correcciones — corregido
  aquí. Nota: el perfil de firma es XAdES-**BES**; FACe exige XAdES-**EPES**
  con `SignaturePolicyIdentifier` — pendiente ese detalle de perfil.
- **Namespace incorrecto**: ~~`FacturaEService.cs:20` usaba
  `http://www.facturae.gob.es/formato/Version3.2.2/Facturae32.xsd`~~
  **✅ Corregido** — ahora usa
  `http://www.facturae.gob.es/formato/Versiones/Facturaev3_2_2.xml`, el
  namespace real de la versión 3.2.2 (confirmado contra la publicación
  oficial de facturae.gob.es).
- **Validación circular del `FacturaEXmlStructureValidator`**: ~~el validador
  comprobaba el namespace contra la misma constante equivocada que usaba el
  generador~~ **✅ Corregido** — el namespace correcto ahora vive como única
  constante en `FacturaEXmlStructureValidator.FacturaENamespace`, y
  `FacturaEService.cs` la referencia directamente en vez de mantener su
  propio literal — generador y validador ya no pueden volver a divergir
  entre sí. Verificado con build+test completo tras el cambio.
- **Bloque `Extensions` sigue mal formado** dentro de `FileHeader`
  (`FacturaEService.cs:236-264`) con una estructura inventada, no la que
  define el estándar (que exige contenido de un namespace ajeno, no hijos
  con nombres inventados).
- **NIF sin validar**: ~~se pasa tal cual~~ **✅ Corregido** —
  `SpanishTaxIdValidator` en `FacturaEService.ValidateTaxId` se invoca en
  emisión de factura; el bug de inversión letra/dígito en CIF (ver "Hallazgo
  sistémico" más abajo) ya está corregido, así que un CIF de S.A./S.L. real
  ya no se rechaza aquí.
- **Dirección con placeholders hardcodeados**: ~~`PostCode="00000"`,
  `Town="N/D"`, `Province="N/D"`~~ **🟡 Parcial** — `ExtractTown`/`ExtractPostCode`
  parsean la dirección; `Province` sigue como `N/D` si no hay dato.
- **Envío a FACe**: **🟡 Parcial+** — `POST
  /api/v1/billing/facturae/{id}/submit-face` + `IFaceSubmissionService`: SOAP
  validado con `FaceSoapStructureValidator` y POST HTTP opcional (`Face:SendEnabled=true`).
  Homologación offline: `GET .../validate` + `FacturaEXmlStructureValidator`
  (namespace y circularidad ya corregidos, ver arriba). Pendiente: perfil
  XAdES-EPES, bloque `Extensions`, homologación entorno test.
- Lo que sí está bien: la aritmética de IVA/IRPF por línea y su agregación
  (`FacturaEService.cs:65-71,151,163`) es correcta; el orden general de
  bloques sigue razonablemente la forma del estándar.

### Hallazgo sistémico transversal — validación NIF/CIF/NIE
`SpanishTaxIdValidator` (`Erp.Application/Common/Validation/SpanishTaxIdValidator.cs`)
está centralizado y se invoca en alta/edición cliente y proveedor
(FluentValidation), registro empresa, `UpdateCompanyHandler`, emisión de
factura (`BillingHandlers`) y generación FacturaE — el hueco sistémico de
"cero validación en ningún sitio" está cerrado.

- **NIF y NIE están bien**: `ValidateNif` usa la tabla oficial de 23
  caracteres `"TRWAGMYFPDXBNJZSQVHLCKE"[numero % 23]`; verificado con
  ejemplo manual (`12345678 % 23 = 14 → 'Z'` → `12345678Z`, correcto). NIE
  mapea X/Y/Z a 0/1/2 y reutiliza el mismo cálculo — correcto.
- **CIF tenía la lógica de letra/dígito de control invertida**: ~~las letras
  de tipo de entidad `A, B, E, H` (que legalmente llevan **dígito** de
  control) recibían una **letra** calculada; y `P, Q, S` (que legalmente
  llevan **letra**) recibían un **dígito**. El CIF real de Banco Santander
  `A39000013` (control real `3`) era rechazado, ya que el código esperaba
  `C`.~~ **✅ Corregido** — `ValidateCif` ahora exige dígito para
  `A/B/E/H`, letra para `P/Q/S`, y acepta cualquiera de los dos para el
  resto de prefijos (`C/D/F/G/J/N/R/U/V/W`, que es la regla real para esos
  casos ambiguos). Re-verificado con cálculo manual: `A39000013` (Banco
  Santander) y `A28015865` (Telefónica) ya se aceptan.
- **El test unitario que daba falsa confianza también se corrigió**: ~~`SpanishTaxIdValidatorTests.FindValidCif`
  generaba un CIF por fuerza bruta hasta que el propio validador (ya roto)
  lo aceptaba~~ **✅ Corregido** — sustituido por casos con los dos CIFs
  reales conocidos de arriba, más casos de rechazo con el dígito de control
  alterado deliberadamente. También se eliminó `tmp-find-cif.cs`, un
  fichero suelto de depuración que hacía el mismo cálculo por fuerza bruta
  y no debía estar commiteado.
- Verificado con build+test completo tras el cambio (43 tests, incluidos
  los nuevos casos de CIF).

Pendiente: cobertura en todos los puntos de entrada (p. ej. importaciones
masivas) y leads (sin campo NIF hoy).

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
- No existe una tabla de auditoría dedicada a envíos **SII** más allá del log
  de aplicación. **VeriFactu** sí tiene `VerifactuSubmissionLog` y
  `GET /api/invoices/{id}/verifactu/submissions` (#0a).
- `TaxReport` (`Erp.Domain/Entities/Tax/TaxReport.cs`) existe como entidad
  pero no se localizó un controlador/handler que la persista activamente —
  posible funcionalidad incompleta o pendiente de conectar; no se debe asumir
  que hay un flujo de reporting fiscal automático detrás de esa entidad sin
  verificarlo de nuevo antes de usarla.

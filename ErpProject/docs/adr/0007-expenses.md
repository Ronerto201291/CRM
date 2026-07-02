# ADR-0007: Expenses

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
Cualquier PYME acumula tickets y facturas de gasto (dietas, suministros, compras
menores) que hoy en día se capturan a mano: foto del ticket, copia de los datos a
un Excel o a un programa de contabilidad, y más tarde conciliación con el
proveedor. Es un proceso lento y propenso a errores, y en España además exige
cumplir con requisitos fiscales concretos (base imponible, tipo de IVA, IRPF de
profesionales, hash de integridad por la Ley Antifraude 11/2021).

El módulo `Expenses` del backend (`backend/Modules/Expenses/`) resuelve esto
digitalizando la captura: cualquier persona de la empresa puede fotografiar un
ticket con el móvil y subirlo sin necesidad de credenciales, un proceso en
segundo plano lo pasa por OCR (Tesseract, local, sin servicios cloud de pago) y
extrae los campos fiscales relevantes, y un usuario autenticado revisa/corrige
el borrador antes de aprobarlo. Al aprobar, el documento queda bloqueado y
dispara eventos de dominio que otros módulos consumen (contabilidad, CRM,
inventario). El README del proyecto lo presenta como la funcionalidad
insignia ("Smart Expense Capture"); este ADR documenta lo que existe realmente
en el código, no solo la aspiración descrita ahí.

## Decisión

### Backend
El módulo sigue el mismo patrón modular del resto del monolito (Api /
Application / Domain / Infrastructure, CQRS con MediatR). `ExpensesController`
(`backend/Modules/Expenses/Api/Controllers/ExpensesController.cs`) expone,
todos bajo `api/expenses`:

- `POST /upload/{token}` — **`[AllowAnonymous]`**, límite de 10 MB, valida
  `Content-Type` (jpeg/png/webp/pdf) y además la firma binaria real del
  fichero (magic bytes) para evitar que un archivo malicioso se disfrace de
  imagen. Resuelve el tenant buscando una `Company` cuyo
  `PublicUploadToken` coincida con el token de la URL y que tenga
  `QrUploadEnabled = true` (consulta con `IgnoreQueryFilters()`, porque en
  este punto todavía no hay tenant en el `ITenantContext`). Sube el fichero a
  MinIO/S3 vía `IFileStorageService` y crea un `ExpenseUpload` en estado
  `Pending`, más una entrada en `ActivityLog` del módulo CRM.
- `GET /uploads` — lista de `ExpenseUpload` (autenticado).
- `POST /` (`CreateManual`) — alta manual de un `ExpenseDocument` en estado
  `Draft`, sin pasar por OCR (`OcrConfidence = null`).
- `GET /`, `GET /{id}` — listado y detalle de `ExpenseDocument`.
- `PUT /{id}` (`UpdateDraft`) — corrige los campos del borrador y sustituye
  sus líneas; pasa el documento a `Reviewed`. Falla si `IsLocked`.
- `POST /{id}/lines`, `PUT /{id}/lines/{lineId}`, `DELETE /{id}/lines/{lineId}`
  — gestión de líneas individuales, también bloqueada si `IsLocked`.
- `POST /{id}/approve` — aprueba el documento (ver flujo más abajo).
- `GET /stats` — contadores agregados para el dashboard.

No existe ningún endpoint de "rechazo" (`reject`) en el controlador, aunque el
frontend lo invoca (ver más abajo, sección Consecuencias).

Todos los endpoints salvo `upload/{token}` requieren `[Authorize]`. El
middleware `TenantResolverMiddleware`
(`backend/Erp.Infrastructure/Tenancy/TenantResolverMiddleware.cs`) tiene
`/api/expenses/upload` en su lista de rutas públicas explícitamente comentada
como "Público pero con token", coherente con lo que hace el controlador.

**OCR**: `OcrBackgroundService`
(`backend/Erp.Infrastructure/Services/OcrBackgroundService.cs`) es un
`BackgroundService` que cada 30 segundos toma hasta 5 `ExpenseUpload` en
estado `Pending`, descarga el fichero de MinIO, y ejecuta el binario
`tesseract` (idioma `spa`) pidiendo salida `txt` y `hocr`. De la salida hOCR
calcula una confianza media (`x_wconf`) sobre 0-100 que guarda en
`ExpenseDocument.OcrConfidence` (por debajo de 70 se marca como baja
confianza en el log). Con expresiones regulares extrae CIF/NIF del proveedor,
base imponible, total, número de factura y fecha, deriva el IVA (`Total -
TaxBase`) cuando puede, e intenta reconstruir las líneas de detalle
(patrones de 3 y 4 columnas con validación de coherencia `cantidad × precio ≈
total` con un margen del 5%). Si detecta un CIF que no existe todavía como
`Supplier` en el módulo CRM, **crea el proveedor automáticamente**
(`crmDb.Suppliers.Add(...)`) y registra la creación en `ActivityLog`. Cada
intento de OCR tiene reintentos con backoff exponencial (hasta 3 intentos);
si todos fallan, el `ExpenseUpload` pasa a `Error`. El resultado, sea cual
sea, se persiste como un `ExpenseDocument` en estado `Draft`.

**Aprobación**: `ApproveExpenseHandler`
(`Application/Features/Expenses/Handlers/ExpenseCommandHandlers.cs`) marca el
documento como `Approved`, `IsValidated = true`, `IsLocked = true`, calcula un
`HashSignature` SHA-256 sobre los campos fiscales clave (cumpliendo con el
requisito de integridad de la Ley Antifraude 11/2021), escribe un `AuditLog`
también con su propio hash, y publica un `ExpenseApprovedEvent` vía MediatR
`IPublisher`. Este evento también está mapeado en `OutboxRelayService` y
`ErpEventConsumerService` con la routing key `expense.approved`, es decir,
además de los `INotificationHandler` en memoria, se emite al Outbox
transaccional para distribución por RabbitMQ.

### Frontend
La parte autenticada vive bajo `frontend/src/app/expenses/`:

- `page.tsx` — listado de `ExpenseDocument` con tarjetas de estadísticas
  (pendientes, aprobados, IVA soportado total, base total), filtro por
  estado, un modal de alta manual con auto-cálculo de IVA/total, y un modal de
  revisión donde se editan los campos extraídos por OCR antes de aprobar.
  Todas las llamadas van contra `/api/proxy/expenses*`.
- `[id]/page.tsx` — detalle de un documento con datos del proveedor, líneas,
  totales y botones "Aprobar"/"Rechazar" para documentos en `Draft`/`Reviewed`.

El flujo público de captura **no tiene una página propia en el frontend**. En
`frontend/src/app/settings/page.tsx` se genera la URL pública como
`{origin}/api/expenses/upload/{token}` y se renderiza como imagen QR usando el
servicio externo `api.qrserver.com` (no hay generación local de QR). Esa URL
apunta al propio origen del frontend, pero en `frontend/src/app` no existe
ninguna ruta `api/expenses/upload/[token]` (el único route handler bajo
`src/app/api` es el proxy genérico `api/proxy/[...path]/route.ts`, que exige
cookies de sesión salvo para una lista corta de rutas públicas de auth). En la
práctica, el backend real vive en otro origen (`API_URL`), y la subida es un
`POST` con `multipart/form-data`, no una navegación GET de un enlace — de
modo que escanear el QR con la cámara del móvil no basta por sí solo para
completar la subida sin una interfaz cliente dedicada que haga el `POST`. Ver
"Consecuencias" para el detalle de esta discrepancia entre lo documentado en
el README y lo que hay en el código del frontend.

### Modelo de datos
Todas las tablas viven en el esquema PostgreSQL `expenses`
(`ExpensesDbContext`, `HasDefaultSchema("expenses")`):

- **`ExpenseUploads`** — el fichero crudo recibido por el endpoint público:
  `CompanyId`, `PublicTokenUsed`, `FilePath` (object key en MinIO),
  `FileName`, `ContentType`, `Status` (`Pending`/`Processed`/`Error`),
  `Comment`, y FK opcional `ExpenseDocumentId` una vez procesado por OCR.
- **`ExpenseDocuments`** — el documento fiscal, con o sin OCR:
  `CompanyId`, FK opcional a `ExpenseUpload`, campos fiscales
  (`InvoiceNumber`, `IssueDate`, `TaxBase`, `VATRate`, `VATAmount`,
  `IRPFRate`, `IRPFAmount`, `Total`), `OcrRawData` (jsonb) y `OcrConfidence`,
  datos del proveedor (`SupplierTaxId`, `SupplierName`, `SupplierId` como
  referencia "blanda" al CRM sin FK de EF), estado de flujo (`Status`:
  `Draft`/`Reviewed`/`Approved`/`Rejected` — el valor `Rejected` está
  declarado pero ningún comando del backend lo asigna), `IsValidated`,
  `ValidatedAt`, `IsLocked`, `HashSignature`, y `AccountingEntryId` (columna
  de referencia, sin navegación EF).
- **`ExpenseDocumentLines`** — líneas de detalle (`Description`, `Quantity`,
  `UnitPrice`, `VATRate`, `LineTotal`, `ProductId` opcional hacia Inventory,
  `SortOrder`), con FK real de EF y cascada hacia `ExpenseDocument`.
- **`AccountingEntries`** — entidad definida en el módulo (`AccountDebit`,
  `AccountCredit`, `Amount`, `PostedAt`, FK a `ExpenseDocument`) pero, tras
  revisar todos los *handlers* del módulo, **no hay ningún punto del código
  que inserte filas en `AccountingEntries`**; el asiento contable real lo
  genera el módulo `Accounting` en su propia tabla `JournalEntries` (ver
  siguiente sección). `AccountingEntries` está migrada y expuesta en
  `IExpensesDbContext`, pero de facto no se usa.

Aislamiento multi-tenant: `ExpenseUpload`, `ExpenseDocument` y
`AccountingEntry` tienen `HasQueryFilter(e => e.CompanyId ==
TenantContext.TenantId)`; `ExpenseDocumentLine` no tiene `CompanyId` propio y
se protege indirectamente a través de su FK a `ExpenseDocument`.

### Flujo end-to-end representativo
1. Un administrador activa el QR en **Configuración** (`QrUploadEnabled =
   true`) y descarga/comparte la imagen generada a partir de
   `PublicUploadToken`.
2. Alguien del equipo fotografía un ticket y lo sube (vía un cliente que haga
   el `POST multipart/form-data` a `api/expenses/upload/{token}`, sin
   autenticación). El backend valida tipo y firma del fichero, resuelve la
   empresa por el token, guarda el objeto en MinIO y crea un `ExpenseUpload`
   en `Pending`.
3. `OcrBackgroundService`, cada 30 segundos, recoge el `ExpenseUpload`,
   ejecuta Tesseract, calcula la confianza, extrae CIF/base/IVA/total/número
   de factura/fecha y líneas de detalle, crea al vuelo el proveedor en CRM si
   el CIF es nuevo, y persiste un `ExpenseDocument` en `Draft` enlazado al
   upload.
4. Un usuario autenticado abre `/expenses`, ve el documento como pendiente,
   pulsa "Revisar", corrige los campos que el OCR haya interpretado mal
   (`PUT /api/expenses/{id}` → estado `Reviewed`) y pulsa "Aprobar".
5. `POST /api/expenses/{id}/approve` bloquea el documento (`IsLocked`),
   calcula el `HashSignature`, escribe `AuditLog` y publica
   `ExpenseApprovedEvent`. Tres *handlers* reaccionan de forma independiente
   y verificablemente idempotente (cada uno comprueba antes de escribir):
   - `Erp.Modules.Accounting.Application.Handlers.ExpenseApprovedEventHandler`
     genera un `JournalEntry` con líneas de Debe/Haber según el PGC español
     (600 Compras + 472 IVA Soportado + 4700 IRPF Deducible si aplica, contra
     410 Proveedores), verificando que Debe y Haber cuadren antes de guardar.
   - `Erp.Modules.Crm.Application.EventHandlers.ExpenseApprovedActivityHandler`
     crea entradas en `ActivityLog` del CRM (una sobre el gasto, y otra sobre
     el proveedor si `SupplierId` está informado).
   - `Erp.Modules.Inventory.Application.EventHandlers.ExpenseApprovedInventoryHandler`
     incrementa stock y recalcula el coste medio ponderado de los productos de
     las líneas que tengan `ProductId` asociado, registrando el movimiento y
     publicando a su vez `StockMovementCreatedEvent`.
6. El documento queda visible como "Contabilizado" y ya no admite ediciones.

## Relación con otros módulos
- **ADR-0002 (multi-tenancy/auth)**: el endpoint público de subida es el único
  punto de entrada de todo el backend que resuelve el tenant sin JWT —lo hace
  emparejando `Company.PublicUploadToken` + `QrUploadEnabled`, con
  `IgnoreQueryFilters()` porque el filtro global de tenant depende de un
  contexto que aún no existe en ese momento del *pipeline*. El resto de
  endpoints del módulo dependen del filtrado estándar por `CompanyId`.
- **ADR-0004 (CRM)**: dependencia real y bidireccional. `ExpensesController`
  inyecta `ICrmDbContext` directamente para escribir `ActivityLog` en la
  subida; `OcrBackgroundService` crea `Supplier` en CRM cuando detecta un CIF
  nuevo; y `ExpenseApprovedActivityHandler` (en el propio módulo CRM) reacciona
  a `ExpenseApprovedEvent` para dejar traza en el timeline de actividad, tanto
  del gasto como del proveedor.
- **ADR-0006 (Accounting)**: `ExpenseApprovedEventHandler` en `Accounting` es
  el que realmente contabiliza el gasto (asiento en `JournalEntries`),
  desacoplado por evento de dominio — la entidad `AccountingEntry` que vive
  dentro del propio módulo Expenses no se usa para esto pese a su nombre y
  comentario ("Accounting entry auto-generated on expense approval").
- **Inventory**: no mencionado explícitamente en el enunciado de este ADR pero
  verificado en código — `ExpenseApprovedInventoryHandler` consume el mismo
  evento para dar entrada de stock a productos comprados, con FIFO/CMP de
  coste medio ponderado.
- **Purchasing**: no se ha encontrado ninguna referencia a `Expenses` dentro de
  `backend/Modules/Purchasing`; el alta automática de proveedor descrita en el
  README ocurre contra la entidad `Supplier` del módulo **CRM**
  (`ICrmDbContext`), no contra Purchasing.

## Buenas prácticas aplicables
- **Validación de contenido real, no solo de cabecera**: el endpoint público
  comprueba la firma binaria (magic bytes) además del `Content-Type`
  declarado por el cliente, mitigando *content-type spoofing* en un endpoint
  sin autenticación.
- **Idempotencia en consumidores de eventos**: los tres *handlers* de
  `ExpenseApprovedEvent` comprueban antes de escribir (por
  `SourceId`/`ReferenceId`/`Action`), de forma que reintentos del Outbox o
  redeliveries de RabbitMQ no dupliquen asientos, movimientos de stock ni
  actividades de CRM.
- **Inmutabilidad tras aprobación**: `IsLocked` se comprueba en todos los
  comandos de edición (`UpdateDraft`, `AddLine`, `UpdateLine`, `DeleteLine`)
  antes de tocar un documento aprobado, y el hash SHA-256 sobre los campos
  fiscales deja rastro de manipulación posterior, en línea con la Ley
  Antifraude 11/2021.
- **Reintentos con backoff en trabajos en segundo plano**: `OcrBackgroundService`
  reintenta hasta 3 veces con espera exponencial antes de marcar un upload
  como `Error`, evitando perder documentos por fallos transitorios de
  Tesseract o de la descarga desde MinIO.
- **Ausencia notable de validación declarativa**: a diferencia de otros
  módulos del backend, no se ha encontrado ningún validador FluentValidation
  en `Modules/Expenses`; los comandos (`CreateExpenseDocumentCommand`,
  `UpdateExpenseDraftCommand`, etc.) no validan rangos de IVA (21/10/4/0),
  coherencia `Base + IVA − IRPF = Total`, ni formato del CIF/NIF más allá de
  lo que hace la extracción por regex del propio OCR.

## Consecuencias
- **Riesgo de precisión del OCR**: la extracción usa expresiones regulares
  sobre texto plano de Tesseract, sin un modelo de layout entrenado; el
  parser de líneas exige patrones de columnas bastante rígidos (separación
  por 2+ espacios) y tolera solo un 5% de descuadre en `cantidad × precio ≈
  total`. Tickets con maquetación distinta a la esperada probablemente no
  generarán líneas de detalle, aunque sí puede extraerse el total y la base
  globales.
- **Superficie de ataque del endpoint público**: `upload/{token}` es
  deliberadamente anónimo; su única defensa es el conocimiento del token (que
  puede regenerarse desde Configuración) más el límite de 10 MB y la
  validación de firma de fichero. No hay *rate limiting* visible en el propio
  controlador para este endpoint.
- **Brecha entre lo documentado y el frontend real**: el README describe
  "Escanear QR con el móvil → subir foto del ticket" como un flujo de dos
  pasos, pero no existe una página en `frontend/src/app` que sirva ese
  formulario de subida en la URL que se genera y publica en el QR
  (`/api/expenses/upload/{token}` contra el origen del frontend, que no tiene
  ruta registrada para ello). El endpoint del backend existe y funciona por
  `POST` directo, pero requiere un cliente propio (app, `curl`, formulario
  dedicado) que hoy no está en este repositorio.
- **Inconsistencias menores frontend/backend detectadas al leer el código**:
  `frontend/src/app/expenses/[id]/page.tsx` invoca `POST
  /api/expenses/{id}/reject`, endpoint que no existe en
  `ExpensesController` (solo hay `approve`); y esa misma página espera
  campos `doc.uploads` y `doc.ocrData` que `ExpenseDocumentDetailDto` no
  expone (el campo real es `ocrRawData`, y no hay lista de `Uploads`
  embebida en el detalle). Ambos casos degradan silenciosamente sin romper
  el resto de la página.
- **Entidad muerta**: `AccountingEntry`/`AccountingEntries` está migrada,
  mapeada en el `DbContext` y expuesta por `IExpensesDbContext`, pero ningún
  *handler* la usa; el asiento contable real se modela en el módulo
  `Accounting`. Mantenerla sin uso es deuda técnica menor pero puede confundir
  a quien la lea esperando encontrar ahí el asiento.
- **Beneficio principal**: pese a los huecos anteriores, el flujo backend
  Upload → OCR → Draft → Revisión → Aprobación → (Accounting + CRM +
  Inventory) está completo, es asíncrono, e integra correctamente varios
  módulos por eventos de dominio con idempotencia, lo que da una base sólida
  una vez se cierre la pieza de UI de captura pública y se añadan
  validaciones declarativas a los comandos.

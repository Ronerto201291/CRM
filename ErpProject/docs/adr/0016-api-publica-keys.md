# ADR-0016: API Pública y API Keys

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El ERP expone una API pública versionada (`/api/v1/...`) pensada para
integraciones externas (contabilidad, e-commerce, scripts propios de un
tenant), autenticada por **API Key** en vez de por el JWT de usuario que
usa el resto de la aplicación (ADR-0002). Existía un documento
`ErpProject/PUBLIC_API_DOCUMENTATION.md` con la referencia de esta API,
pero con codificación de caracteres corrupta (mojibake) y contenido
parcialmente aspiracional respecto al código real. Esta ADR migra su
contenido útil, verificado línea a línea contra los controllers reales, y
documenta también un hallazgo importante: **hay dos rutas de API pública
con niveles de protección distintos**, y no todo lo documentado
originalmente está realmente protegido tal como se afirmaba.

## Decisión

### Backend — dos superficies públicas distintas
1. **`PublicApiController`** (`backend/Erp.Api/Controllers/PublicApiController.cs`,
   ruta `api/v1`, versión `1.0` vía `Asp.Versioning`): endpoints de
   facturas, clientes y reportes contables (Diario, Mayor, Balance, PyG)
   pensados como "API pública general". **No tiene atributo
   `[Authorize]`** a nivel de clase ni de la mayoría de sus acciones
   (solo `Health()` lleva `[AllowAnonymous]` explícito, lo cual solo
   tiene sentido si el resto requiriera auth por defecto). Su constructor
   inyecta `IApiKeyValidator` para el endpoint `GET /auth/verify`.
2. **`PublicInvoicesController`** y **`PublicQuotesController`**
   (`backend/Modules/Billing/Api/Controllers/Public/V1/`, rutas
   `api/v{version}/public/invoices` y `api/v{version}/public/quotes`):
   endpoints de solo facturas (lectura) y de portal público de
   presupuestos por token de aceptación (sin API key, pensado para que
   el cliente final abra un enlace de email).

**Middleware realmente registrado** en `Program.cs` (línea ~315):
`application.UseMiddleware<Erp.Infrastructure.Security.ApiKeyRateLimitMiddleware>()`.
Este middleware (`backend/Erp.Infrastructure/Security/ApiKeyRateLimitMiddleware.cs`)
solo actúa sobre rutas que empiezan por **`/api/v1/public`**: exige
cabecera `X-Api-Key`, calcula su hash SHA-256, busca un `ApiKey` activo
en `ErpDbContext.ApiKeys` (`IgnoreQueryFilters`, porque las keys son
globales) y aplica un rate limit por minuto contra Redis
(`ratelimit:apikey:{id}:{yyyyMMddHHmm}`, expira a los 70s), devolviendo
`429` con cabeceras `X-RateLimit-*` y `Retry-After` si se excede. Cada
petición válida se registra como fila `ApiUsageLog`.

Existe una **segunda implementación independiente**,
`ApiKeyValidationMiddleware` + `ApiKeyValidator`
(`backend/Erp.Infrastructure/Security/ApiKeyValidationMiddleware.cs` y
`ApiKeyValidator.cs`), pensada para proteger `/api/v1` en general
(excepto `/api/v1/health`). Esta segunda implementación:
- Valida contra información cacheada **solo en Redis**
  (`ApiKeyInfo` serializado bajo `api_key:{hash}`), con sus propios
  métodos `CreateApiKeyAsync`/`RevokeApiKeyAsync` que nunca tocan la
  tabla `ApiKey` de la base de datos — es un almacén de claves
  completamente distinto y desconectado del que usan `ApiKeysController`
  y `ApiKeyRateLimitMiddleware`.
- **No está registrada en `Program.cs`** (`UseMiddleware<ApiKeyValidationMiddleware>`
  no aparece en el pipeline) ni **`IApiKeyValidator`/`ApiKeyValidator`
  están registrados en el contenedor de DI** (no se ha encontrado
  `AddScoped<IApiKeyValidator, ApiKeyValidator>` ni equivalente en todo
  el backend).

**Consecuencia verificable en código de ambos hallazgos combinados:**
`PublicApiController` depende en su constructor de `IApiKeyValidator`,
que no tiene registro en DI, por lo que cualquier intento de invocar una
acción de ese controller (incluida `Health()`, porque la activación del
controller falla antes de llegar a la acción) fallaría en tiempo de
ejecución si el contenedor de DI no puede resolver esa dependencia. Y
como el middleware que sí protege por API Key
(`ApiKeyRateLimitMiddleware`) solo cubre `/api/v1/public/**`, las rutas
de `PublicApiController` (`/api/v1/invoices`, `/api/v1/clients`,
`/api/v1/reports/...`) no están cubiertas por ningún middleware de
API Key en el pipeline actual. La superficie que sí funciona de forma
consistente end-to-end (DB real, middleware registrado, rate limit
operativo) es `PublicInvoicesController`
(`/api/v1/public/invoices`).

**Gestión de claves — `ApiKeysController`**
(`backend/Erp.Api/Controllers/ApiKeysController.cs`, ruta
`api/ApiKeys`, `[Authorize]`, requiere JWT normal de usuario, no API
key): `GET` (listar claves del tenant), `POST` (crear — genera 32 bytes
aleatorios en Base64 URL-safe, guarda `KeyHash` SHA-256 y muestra la
clave completa **una sola vez** en la respuesta), `DELETE /{id}`
(revocar, pone `IsActive = false`). Entidades:
`ApiKey` (`CompanyId`, `Name`, `KeyPrefix` —8 primeros caracteres, solo
para mostrar—, `KeyHash`, `IsActive`, `RateLimit`) y `ApiUsageLog`
(`ApiKeyId`, `Endpoint`, `Timestamp`) en el esquema `public`.

### Frontend
`frontend/src/app/settings/api-keys/page.tsx`: lista claves
(`GET /api/proxy/api-keys`), formulario de creación con nombre y rate
limit (`POST`), modal que muestra la clave completa una única vez tras
crearla, y revocación. Consume la API real de `ApiKeysController`
(no la de `IApiKeyValidator`).

### Referencia de la API pública (contenido migrado y verificado)
Autenticación: cabecera `X-API-Key: sk_live_...` en cada request a
`/api/v1/public/...`. Base URLs (según el documento original, no
verificables desde el código): producción
`https://api.tudominio.com/api/v1`, desarrollo `http://localhost:5000/api/v1`.

- `GET /api/v1/health` — sin autenticación, devuelve
  `{ status, timestamp, version }`. Verificado en
  `PublicApiController.Health()`.
- `GET /api/v1/auth/verify` — valida la API Key vía `IApiKeyValidator`
  (cabecera `X-API-Key`) y devuelve `{ message, status }`. **Nota de
  verificación:** dado que `IApiKeyValidator` no tiene registro de DI
  (ver arriba), este endpoint concreto no es fiable tal como está el
  código hoy.
- `GET/POST /api/v1/invoices`, `GET /api/v1/invoices/{id}`,
  `POST /api/v1/invoices/{id}/pay`, `POST /api/v1/invoices/{id}/lock` —
  verificados en `PublicApiController`. El documento original describía
  paginación (`page`, `pageSize`, `totalCount`) y un `PATCH /invoices/{id}`
  que **no existen** en el controller actual — el código real no pagina
  (devuelve la lista completa de `GetInvoicesQuery`) y no expone edición
  de factura por esta vía.
- `GET/POST/PATCH /api/v1/clients`, `GET /api/v1/clients/{id}` —
  verificados en `PublicApiController` (`PATCH` sí existe para clientes,
  a diferencia de facturas).
- `GET /api/v1/reports/diario|mayor|balance|pyg` — verificados,
  requieren `fechaInicio`/`fechaFin` o `fechaCorte` como query string en
  formato fecha.
- **Superficie recomendada para integraciones reales:**
  `GET /api/v1/public/invoices` y `GET /api/v1/public/invoices/{id}`
  (`PublicInvoicesController`), porque son las únicas efectivamente
  protegidas por `ApiKeyRateLimitMiddleware` con rate limit operativo.
- Rate limiting: por minuto, específico de cada `ApiKey.RateLimit`;
  respuesta `429` con `Retry-After: 60` y cabeceras
  `X-RateLimit-Limit`/`X-RateLimit-Remaining` — verificado en
  `ApiKeyRateLimitMiddleware`.

## Relación con otros módulos
- **ADR-0002 (autenticación):** la API pública es una vía de acceso
  alternativa al JWT de usuario — autentica por posesión de una API Key
  válida y hasheada, no por sesión de usuario. `ApiKeysController` (la
  gestión de claves) sí usa JWT normal, porque lo opera un usuario
  logueado del tenant.
- **ADR-0005 Billing:** `PublicInvoicesController` reexpone en solo
  lectura una vista simplificada de `Invoice` para integraciones
  externas.
- Cada `ApiKey` está ligada a un único `CompanyId`: toda operación hecha
  con esa clave queda implícitamente scoped a ese tenant.

## Buenas prácticas aplicables
- Cualquier controller nuevo de "API pública" debe montarse bajo
  `/api/v1/public/...` para quedar cubierto por
  `ApiKeyRateLimitMiddleware`, siguiendo el patrón de
  `PublicInvoicesController` (inyectar el DbContext del módulo
  correspondiente, sin volver a resolver la API Key manualmente).
- No usar `IApiKeyValidator`/`ApiKeyValidator` para nuevo código: es una
  segunda implementación no conectada a la tabla `ApiKey` real ni
  registrada en DI; el camino soportado es
  `ApiKeyRateLimitMiddleware` + `ErpDbContext.ApiKeys`.
- Antes de recomendar `PublicApiController` (`/api/v1/invoices`,
  `/api/v1/clients`, `/api/v1/reports/...`) a un integrador externo,
  habría que decidir explícitamente si se le añade `[Authorize]`/gate
  de API Key real o si se retiran esas rutas en favor de las
  equivalentes bajo `/api/v1/public/...`.

## Consecuencias
- **Unificado jul 2026:** `ApiKeyRateLimitMiddleware` protege todo `/api/v1/**`
  excepto `health` y el portal de presupuestos por token (`/api/v1/public/quotes/**`).
  Valida contra `ErpDbContext.ApiKeys`, aplica rate limit Redis, fija
  `ITenantContext` desde `ApiKey.CompanyId` y registra `ApiUsageLog`.
  El middleware se ejecuta **antes** de `MapControllers` (bug corregido: antes
  estaba después y no interceptaba rutas).
- Eliminados `IApiKeyValidator`, `ApiKeyValidator` (Redis huérfano) y
  `ApiKeyValidationMiddleware` (no registrados). `PublicApiController` ya no
  depende de `IApiKeyValidator`; `GET /auth/verify` confirma la clave ya
  validada por el middleware.
- `PublicInvoicesController` (`/api/v1/public/invoices`) sigue siendo la
  superficie recomendada para integraciones de solo lectura; las rutas de
  `PublicApiController` (`/api/v1/invoices`, `/api/v1/clients`, reportes)
  comparten el mismo gate de API Key. Los endpoints de reportes enlazan
  directamente `GetDiarioQuery` / `GetMayorQuery` / etc. desde query string;
  get-by-id usa `GetInvoiceByIdQuery` y `GetClientByIdQuery` (sin filtrar
  listas en el controller).
- El documento `PUBLIC_API_DOCUMENTATION.md` original puede seguir
  divergiendo en detalles (paginación, PATCH factura); esta ADR es la
  referencia verificada contra código.

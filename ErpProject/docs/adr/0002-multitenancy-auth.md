# ADR-0002: Multi-tenancy y autenticación

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El ERP es multi-tenant: una única instancia de la aplicación y de la base de
datos sirve a múltiples empresas (`Company`), aislando los datos de cada una
por `CompanyId`. Todo módulo de negocio (CRM, Billing, Accounting...) depende
de esta capa para saber "de qué empresa son los datos que se están leyendo o
escribiendo" y "qué puede hacer el usuario actual". El código relevante vive
principalmente en `backend/Erp.Api/Controllers/{AuthController,UsersController,
PermissionsController,CompanyController}.cs`,
`backend/Erp.Application/Features/{Auth,Users}/**`,
`backend/Erp.Domain/Entities/Core/**`,
`backend/Erp.Infrastructure/{Security,Tenancy}/**`, y en el frontend
`frontend/src/context/TenantContext.tsx` + `frontend/src/middleware.ts`.

## Decisión

### Backend
**Autenticación.** `AuthController` (`api/auth`) expone, entre otros:
`POST /login`, `POST /register` (alta autónoma de empresa),
`POST /accept-invite`, `POST /refresh`, `POST /2fa/setup`,
`POST /2fa/confirm`, `POST /2fa/disable`, `POST /2fa/verify`,
`POST /forgot-password`, `POST /reset-password`,
`POST /resend-confirmation`, `GET /confirm-email`.

- `LoginCommand` → `LoginCommandHandler`
  (`backend/Erp.Application/Features/Auth/Handlers/LoginCommandHandler.cs`):
  busca el usuario por email **ignorando el filtro de tenant**
  (`IgnoreQueryFilters()`, porque el tenant aún no está resuelto en el
  login), verifica la contraseña con BCrypt (sin fallback en texto plano),
  y si `user.TwoFactorEnabled` es `true` devuelve `RequiresTwoFactor = true`
  sin emitir JWT todavía; si no, emite el JWT directamente.
- El JWT lo genera `JwtProvider.Generate(user)`
  (`backend/Erp.Infrastructure/Security/JwtProvider.cs`) con claims `sub`
  (UserId), `email`, `CompanyId` y `name`, firmado HMAC-SHA256, válido 8
  horas (`DateTime.UtcNow.AddHours(8)`).
- `RefreshTokenCommand` / `RefreshTokenHandler` implementan rotación de
  refresh tokens: busca un `RefreshToken` no revocado y no expirado,
  lo marca `IsRevoked = true` y emite un access token + refresh token
  nuevos. La entidad `RefreshToken`
  (`backend/Erp.Domain/Entities/Core/RefreshToken.cs`) guarda
  `UserId`, `Token`, `ExpiresAt`, `IsRevoked`, `ReplacedByToken`.
- 2FA: `TwoFactorCommands` + `TwoFactorHandlers`
  (`backend/Erp.Application/Features/Auth/{Commands/TwoFactorCommands.cs,
  Handlers/TwoFactorHandlers.cs}`) implementan TOTP sobre `User.TotpSecret`
  (Base32) y `User.TotpBackupCodes` (JSON de códigos de un solo uso).
- Alta de empresa: dos caminos distintos en
  `backend/Erp.Application/Features/Auth/Commands/`:
  - `RegisterCompanyCommand` (self-service): valida unicidad de CIF/NIF y
    email, crea `Company` + `Subscription` (plan Free) + usuario admin, y
    devuelve JWT para auto-login inmediato.
  - `InviteCompanyCommand` / `AcceptInviteCommand`: crea una `Company` con
    `TaxId = "PENDING-..."` y una `Subscription` en modo `trialing`
    (plan "Pro", 1 mes), genera una fila `TenantInvitation`
    (`Email`, `Token`, `ExpiresAt`, `IsUsed`) y un enlace de invitación;
    `AcceptInviteCommand` consume el token y completa el alta del usuario.
- Protección de fuerza bruta: `LoginRateLimitMiddleware`
  (`backend/Erp.Infrastructure/Security/LoginRateLimitMiddleware.cs`),
  registrado tras `TenantResolverMiddleware`, limita a 10 intentos por IP
  por minuto vía Redis (según el README; confirmar límite exacto en el
  propio archivo si se modifica).

**Usuarios y roles.** `UsersController` (`api/users` — inferido de
`[controller]`) expone `GET /` (listar usuarios), `GET /roles`,
`POST /` (alta de usuario); los comandos/queries viven en
`backend/Erp.Application/Features/Users/{Commands/UserCommands.cs,
Queries/UserQueries.cs,Handlers/UserHandlers.cs}`. Cada `User`
(`backend/Erp.Domain/Entities/Core/User.cs`) pertenece a una `Company`
(`CompanyId`) y tiene un único `Role` opcional vía `RoleId`.

**Permisos (RBAC + ABAC).** El modelo combina roles con reglas de
grano fino:
- `Role` (`backend/Erp.Domain/Entities/Core/Role.cs`): pertenece a una
  `Company`, tiene un `Name` y una colección de `RolePermission`.
- `Permission` (`backend/Erp.Domain/Entities/Core/Permission.cs`): definido
  por `Resource` + `Action` (p. ej. `"Invoice"` + `"Approve"`), con un
  `Code`/`Description` heredados por compatibilidad.
- `RolePermission` y `UserPermission` conectan roles/usuarios con permisos;
  `UserPermission` admite `IsGranted` (concesión explícita) o denegación
  explícita, y `ExpiresAt` opcional.
- `PermissionService`
  (`backend/Erp.Infrastructure/Security/PermissionService.cs`) resuelve el
  conjunto efectivo de permisos de un usuario con esta prioridad:
  1. Denegación explícita a nivel de usuario → deniega siempre.
  2. Concesión explícita a nivel de usuario → permite.
  3. Permiso heredado del rol del usuario (`RolePermissions` por
     `RoleName`) → permite si no está denegado en el paso 1.
  4. Por defecto: denegado.
  El resultado se cachea en Redis 5 minutos por usuario
  (`permissions:user:{userId}`).
- La autorización se aplica en dos niveles, ambos como filtros MVC
  registrados en `Program.cs`:
  1. **`ModuleAuthorizationFilter`** (RBAC a nivel de módulo, vía
     `[RequiredModule]`): usa `ModuleAuthorizationHandler`
     (`backend/Erp.Infrastructure/Security/ModuleAuthorizationHandler.cs`)
     para comprobar, en este orden, que (a) hay tenant resuelto, (b) su
     `Subscription` está activa y no caducada, (c) el `Plan` contratado
     incluye ese módulo, y (d) el `TenantModule` del tenant tiene
     `IsEnabled = true` para ese módulo.
  2. **`AbacAuthorizationFilter`** (ABAC a nivel de acción, vía
     `[RequirePermission(Resource, Action)]`,
     `backend/Erp.Infrastructure/Security/AbacAuthorizationFilter.cs`):
     se ejecuta **después** del filtro de módulo (registrado en
     `Program.cs` con `options.Filters.AddService<AbacAuthorizationFilter>()`)
     y llama a `PermissionService.HasPermissionAsync(resource, action)`,
     devolviendo 403 con `code = "PERMISSION_DENIED"` si falla.
- `PermissionsController` (`api/permissions`) expone `GET /` (catálogo),
  `GET /my` (permisos efectivos del usuario actual), `POST /grant`,
  `POST /revoke`, `POST /deny`.

**Multi-tenancy.** `CompanyController` (`api/company`) expone
`GET /` (datos de la empresa actual), `PUT /` (actualización) y
`POST /regenerate-token` (rota el `PublicUploadToken` usado para el QR de
Smart Expense Capture). La entidad `Company`
(`backend/Erp.Domain/Entities/Core/Company.cs`) tiene `Name`, `TaxId`,
`Address`, `Country` (por defecto `"ES"`), `SubscriptionId`, `IsActive`,
`PublicUploadToken`/`QrUploadEnabled`, `StripeCustomerId`, y
`MatchingToleranceAmount` (tolerancia de conciliación de compras, por
tenant).

La resolución del tenant por request ocurre en
`TenantResolverMiddleware` (`backend/Erp.Infrastructure/Tenancy/
TenantResolverMiddleware.cs`), registrado **antes** de
`UseAuthentication()`/`UseAuthorization()` en `Program.cs`:
1. Prioriza la cabecera `X-Tenant-Id` (GUID); si está presente pero no
   corresponde a una `Company` activa, responde 401; si el formato no es
   un GUID válido, responde 400.
2. Si no hay cabecera, intenta resolver por subdominio
   (`tenant.dominio.com`, comparando contra `Company.Name` en
   minúsculas) — mecanismo alternativo de SaaS multi-tenant, no usado
   actualmente por el frontend.
3. Si la ruta es "protegida" (todo excepto
   `/api/auth/{login,register,refresh}`, `/api/expenses/upload`,
   `/api/stripe/webhook`, `/health`, `/swagger`, `/hangfire`) y no se
   resolvió tenant, responde 401.
4. Si se resolvió, llama a `tenantContext.SetTenant(id, name)`
   (`ITenantContext`, implementado por `TenantContext`
   — un servicio *scoped*, uno por request).

Ese `ITenantContext` es leído por `ModuleDbContextBase.SaveChangesAsync`
(asignación automática de `CompanyId` en altas) y por los
`HasQueryFilter(e => e.CompanyId == TenantContext.TenantId)` que cada
`DbContext` de módulo aplica a sus entidades — p. ej. en
`CrmDbContext.OnModelCreating`
(`backend/Modules/Crm/Infrastructure/Data/CrmDbContext.cs`):
```csharp
modelBuilder.Entity<Client>().HasQueryFilter(e => e.CompanyId == TenantContext.TenantId);
```
y de forma equivalente en `ErpDbContext` para las entidades core
(`User`, `Role`, `AuditLog`, `Subscription`, `ApiKey`, `TenantModule`,
`TenantInvitation`, `FiscalEvent`, `TaxReport`, `Rule`...). Este filtro
global de EF Core es el mecanismo real de aislamiento de datos: toda
consulta LINQ normal contra estos `DbSet` queda automáticamente acotada a
`CompanyId == TenantContext.TenantId`, salvo que se llame explícitamente a
`.IgnoreQueryFilters()` (usado deliberadamente en login, alta de empresa,
resolución de permisos, y comprobaciones de licencia/módulo, donde el
tenant aún no está resuelto o se necesita mirar "a través" de tenants).

### Frontend
- `frontend/src/context/TenantContext.tsx` (`TenantProvider`/`useTenant`)
  mantiene `tenantId`/`tenantName` en `localStorage`, hidratado en el
  cliente tras el primer render.
- `frontend/src/middleware.ts` es el middleware de Next.js: para rutas
  bajo `/api/proxy/*`, toma las cookies `erp_token` y `tenantId` y las
  reinyecta como cabeceras `Authorization: Bearer <token>` y
  `X-Tenant-Id: <tenantId>` en la petición proxied hacia el backend.
  También protege rutas privadas (`/dashboard`, `/crm`, `/billing`,
  `/accounting`, `/inventory`, `/expenses`, `/settings`) redirigiendo a
  `/admin` si no hay cookie `erp_token`, y redirige usuarios ya
  autenticados lejos de `/admin`, `/register`, `/signup`, `/`.
- Páginas de alta: `frontend/src/app/register/page.tsx` (alta autónoma) y
  `frontend/src/app/signup/page.tsx`. Gestión de usuarios y permisos del
  tenant actual vive bajo `frontend/src/app/settings/` (`users/`,
  `api-keys/`, `audit-logs/`, `automation/`, `empresas/`, `sii/`,
  `subscription/`).

### Modelo de datos
`Company` 1—N `User`, 1—N `Role`. `Role` 1—N `RolePermission` N—1
`Permission`. `User` N—1 `Role` (opcional), 1—N `UserPermission` N—1
`Permission`, 1—N `RefreshToken`. `TenantInvitation` referencia una
`Company` pendiente de completar alta. `TenantModule` (`CompanyId`,
`ModuleName`, `IsEnabled`) es el flag de activación por tenant y módulo
que consulta `ModuleAuthorizationHandler`. Todas las entidades
"core" con datos de negocio llevan `CompanyId` y están sujetas al filtro
global de tenant salvo cuando se ignora explícitamente.

### Flujo end-to-end representativo
1. El usuario envía credenciales en `/admin` (o `/register`); el frontend
   llama a `POST /api/auth/login`.
2. `LoginCommandHandler` valida credenciales e (si no hay 2FA) devuelve
   `LoginResponseDto` con el JWT y `CompanyId`.
3. El frontend guarda el token (cookie `erp_token`) y el `tenantId`
   (`TenantContext.setTenant`, cookie + `localStorage`).
4. En cada navegación a una ruta protegida o llamada a `/api/proxy/*`,
   `middleware.ts` añade `Authorization: Bearer <token>` y
   `X-Tenant-Id: <tenantId>` a la petición hacia el backend.
5. En el backend, `TenantResolverMiddleware` lee `X-Tenant-Id`, valida
   que la `Company` existe y está activa, y fija `ITenantContext.TenantId`
   para el resto del pipeline de ese request.
6. `UseAuthentication()`/`UseAuthorization()` validan el JWT.
   `ModuleAuthorizationFilter` comprueba que el módulo del endpoint está
   licenciado y activo para ese tenant; `AbacAuthorizationFilter`
   comprueba el permiso concreto (`Resource:Action`) vía
   `PermissionService`.
7. El controller despacha el comando/query MediatR; el `DbContext` del
   módulo correspondiente aplica automáticamente
   `WHERE CompanyId = <tenant resuelto>` a toda consulta, gracias al
   `HasQueryFilter` configurado con `ITenantContext`.

## Relación con otros módulos
Literalmente todos los demás módulos de negocio (CRM, Billing, Accounting,
Expenses, Inventory, Payroll, Purchasing, Sales, Treasury, así como Fiscal/
SII/VeriFactu, Suscripciones, Automatización, API pública y Audit Logs)
dependen de esta ADR para dos cosas: (1) el aislamiento de sus datos por
`CompanyId` vía `ITenantContext` + `HasQueryFilter` en su propio
`ModuleDbContextBase`, y (2) la comprobación de permisos
(`[RequirePermission]`) y de licencia de módulo (`[RequiredModule]`) antes
de ejecutar cualquier acción. Ningún módulo de negocio implementa su
propia lógica de autenticación o resolución de tenant: todos reutilizan
este pipeline compartido.

## Buenas prácticas aplicables
- Cualquier query directa contra un `DbSet` debe **no** usar
  `IgnoreQueryFilters()` salvo que exista una razón explícita y
  documentada (login, resolución de permisos, checks de licencia) — de lo
  contrario se rompe el aislamiento entre tenants.
- Cualquier entidad nueva con datos de negocio debe llevar `CompanyId` y
  su propio `HasQueryFilter` en el `DbContext` del módulo, siguiendo el
  patrón ya usado en `CrmDbContext`.
- Cualquier endpoint nuevo de un módulo de negocio debe decorarse con
  `[RequiredModule("<Nombre>")]` y, si aplica, `[RequirePermission(resource, action)]`
  para heredar automáticamente el RBAC + ABAC ya existente en vez de
  reinventar comprobaciones de autorización a mano.
- Al invalidar permisos de un usuario (grant/revoke/deny), recordar que
  `PermissionService` cachea 5 minutos en Redis
  (`permissions:user:{userId}`); si se requiere invalidación inmediata hay
  que borrar esa clave explícitamente.

## Consecuencias
- El aislamiento multi-tenant depende enteramente de que
  `TenantResolverMiddleware` reciba una cabecera `X-Tenant-Id` correcta
  (o un subdominio válido). No se ha encontrado, en el código revisado,
  ninguna comprobación cruzada que confirme que el `CompanyId` embebido en
  el JWT del usuario autenticado coincide con el tenant resuelto por esa
  cabecera — `PermissionService.GetCurrentUserId()` resuelve el usuario
  únicamente a partir del claim `sub` del JWT, sin comparar contra
  `ITenantContext.TenantId`. En la práctica, el frontend siempre envía el
  `tenantId` asociado al login (vía `TenantContext`/cookies), pero esto es
  una convención de cliente, no una validación de servidor. Vale la pena
  revisar/reforzar esta comprobación si se expone la API a clientes no
  controlados por el propio frontend (p. ej. la API pública, ver
  ADR-0016).
- La resolución de tenant por subdominio existe en el middleware pero no
  parece estar en uso por el frontend actual (que siempre usa
  `X-Tenant-Id`); si se retoma habrá que revisar CORS y la configuración
  de DNS/hosts.
- No existen tests automatizados que verifiquen el aislamiento entre
  tenants (ni ningún otro flujo de auth); cualquier cambio en
  `TenantResolverMiddleware`, `ModuleDbContextBase` o `PermissionService`
  debe verificarse manualmente con al menos dos tenants distintos antes de
  desplegar.
- **El modelo `User`↔`Company` es 1:N rígido (un `User.CompanyId` fijo, sin
  tabla `UserCompany`), no soporta multi-empresa real** (un mismo login con
  acceso a varias empresas — caso de uso relevante para gestorías que llevan
  varias pymes clientas). El email de login es único a nivel de toda la
  plataforma (`RegisterCompanyCommand.cs:54-56`), y el `CompanyId` queda
  grabado como claim fijo e inmutable en el JWT al emitirlo
  (`JwtProvider.cs:33`) — no hay concepto de "empresa activa" cambiable en
  sesión. Si se aborda, implica una migración de esquema real (tabla
  `UserCompany` muchos-a-muchos con rol por membresía, relajar la unicidad
  de email a `(Email, CompanyId)`, y rehacer el JWT para llevar una lista de
  empresas accesibles + una activa conmutable). Backlog y justificación de
  negocio completa en `ADR-0018` ítem 42a — no implementar como parte de un
  cambio menor de auth, es un cambio de esquema con decisión de producto
  detrás (modelo de suscripción por empresa vs. por cuenta/gestoría).

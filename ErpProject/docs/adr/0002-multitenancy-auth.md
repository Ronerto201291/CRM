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

- `POST /login` devuelve JWT con `CompanyId` activo y lista de empresas
  accesibles (`CompanyMembershipDto[]`) cuando el usuario tiene varias
  membresías (`UserCompany`).
- `GET /api/auth/companies` — empresas del usuario autenticado.
- `POST /api/auth/switch-company` — emite JWT nuevo con otra empresa activa
  sin re-login (Fase 1, ADR-0018 #42a).
- `POST /api/auth/add-company` — alta de empresa adicional desde cuenta
  existente (`AddCompanyFromAccountCommand`).

### Multi-empresa gestoría — fases (#42a, ADR-0018)

| Fase | Alcance | Estado |
|---|---|---|
| **1** | `UserCompany`, `switch-company`, `add-company`, `TenantMembershipMiddleware`, `CompanySwitcher` en frontend | ✅ Implementado |
| **2** | UI gestoría: dashboard multi-empresa (KPIs agregados, alertas por cliente) | 🟡 Diseño — requiere OK UX |
| **3** | Roles por membresía (`UserCompany.RoleId`) refinados para operador gestoría vs admin cliente | 🟡 Parcial — modelo existe, UX pendiente |
| **4** | **Suscripción gestoría:** plan que cubre N `Company` bajo una cuenta (Stripe metadata + límites) | ❌ Bloqueado producto — ¿plan por Company o por gestoría? |
| **5** | Facturación consolidada SaaS (una factura Stripe por gestoría con desglose por empresa cliente) | ❌ Bloqueado — depende Fase 4 |

No implementar Fases 4–5 sin decisión de negocio sobre modelo de suscripción.
Fase 1 es suficiente para operar varias empresas con el mismo login.

- `LoginCommand` → `LoginCommandHandler`: busca usuario por email con
  `IgnoreQueryFilters()`, verifica BCrypt, soporta 2FA, resuelve empresa
  activa (membresía por defecto o `User.CompanyId` legacy) y emite JWT.
- `JwtProvider` incluye claim `CompanyId` de la **empresa activa** (cambiable
  vía `switch-company` sin re-login).
- `TenantMembershipMiddleware` valida que el usuario autenticado pertenece a
  la empresa del `X-Tenant-Id` / claim activo (Fase 1 #42a).
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
Queries/UserQueries.cs,Handlers/UserHandlers.cs}`. Cada `User` tiene
`CompanyId` legacy (empresa de alta original) y puede tener **varias**
membresías vía `UserCompany` (`UserId`, `CompanyId`, `RoleId` por empresa).

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

  Ambos filtros se registran hoy con `options.Filters.AddService<...>()`
  en `Program.cs` **y** como tipo concreto (`services.AddScoped<...>()`)
  en `Erp.Infrastructure/DependencyInjection.cs` — las dos cosas son
  necesarias porque `AddService<T>()` resuelve `T` de la misma manera que
  `GetRequiredService<T>()`. `ModuleAuthorizationFilter` solo tenía la
  primera parte durante bastante tiempo (registrado únicamente como
  `IAsyncAuthorizationFilter`, nunca como tipo concreto), así que
  `[RequiredModule]` no se ejecutaba nunca en ningún endpoint, ni siquiera
  en los pocos que ya lo declaraban — corregido junto con la aplicación de
  ambos atributos a los 44 controllers de módulo (ADR-0018 #42c). El
  catálogo de `Permissions` (`Erp.Application/Common/Attributes/
  RequirePermissionAttribute.cs`) creció de 7 a ~35 grupos resource:action
  para cubrir los 9 módulos de negocio.
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
  mantiene `tenantId`/`tenantName` en `localStorage`; `CompanySwitcher` en
  layout permite cambiar empresa activa (`switch-company`) sin re-login (#42a).
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
`Company` 1—N `User` (legacy `User.CompanyId`), 1—N `UserCompany` (membresías
muchos-a-muchos con rol por empresa). `Role` 1—N `RolePermission` N—1
`Permission`. Índice único `(Email, CompanyId)` en `User` (email ya no es
único a nivel plataforma entera). `TenantInvitation`, `TenantModule`,
`RefreshToken` como antes.

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

**Accounting** (ADR-0006) — `RegisterCompanyHandler` y
`AddCompanyFromAccountHandler` publican `CompanyCreatedEvent`
(`Erp.Application.Common.Events`) tras crear la `Company`, consumido por
`SeedChartOfAccountsHandler` en Accounting para sembrar el plan contable PGC
(ADR-0018 #0g — hallazgo real corregido: ninguna empresa registrada por el
flujo normal tenía cuentas, así que cualquier cobro de factura fallaba).
Ninguno de los dos módulos referencia el ensamblado del otro.

El mismo `CompanyCreatedEvent` tiene otros dos handlers en el propio
núcleo (`Erp.Application/Features/Licensing/Handlers/`), no en un módulo:
`SeedTenantModulesHandler` activa (`TenantModule.IsEnabled = true`) cada
módulo incluido en el `Plan` de la nueva suscripción, y
`SeedDefaultRolePermissionsHandler` concede permisos por defecto a los
roles Admin/Manager/Contable recién creados (Admin: todos; Manager: todos
salvo `UserManagement:Delete`/`Accounting:Close`; Contable: acceso
completo a recursos financieros, solo lectura al resto). Sin estos dos
handlers, `[RequiredModule]`/`[RequirePermission]` habrían bloqueado a
cualquier empresa dada de alta por `/register` o `/signup` — solo el
bootstrap de desarrollo en `Program.cs` creaba estas filas antes (ADR-0018
#42c).

## Evaluación de calidad arquitectónica
> Metodología en `ADR-0018`.

- **Multi-empresa Fase 1:** ✅ `UserCompany`, switch-company, middleware (#42a).
- **Fases 2–5:** documentadas en Decisión; Fase 4–5 bloqueadas por modelo suscripción gestoría.
- **Seguridad:** validación JWT↔tenant reforzada vía `TenantMembershipMiddleware`;
  API pública sigue requiriendo revisión (ADR-0016).
- **RBAC/ABAC (#42c):** ✅ Corregido — `[RequiredModule]`/`[RequirePermission]`
  aplicados a los 44 controllers de módulo (antes 3/44 y 0/44 respectivamente,
  y el filtro de módulo ni siquiera se ejecutaba por un registro DI
  incompleto). Regresión protegida con
  `ModuleApiControllers_HaveRequiredModuleAttribute` en `Erp.ArchitectureTests`.

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
  reinventar comprobaciones de autorización a mano — los 44 controllers
  de módulo ya lo hacen (ADR-0018 #42c) y
  `ModuleApiControllers_HaveRequiredModuleAttribute` falla en CI si un
  controller nuevo se olvida de `[RequiredModule]`. Si el módulo nuevo no
  tiene todavía una fila `PlanModule` en algún plan, añadirla en la misma
  migración — de lo contrario ningún tenant podrá usarlo nunca (pasó con
  Treasury/Payroll/Purchasing/Sales).
- Si el recurso ABAC que necesitas no existe en `Permissions`
  (`RequirePermissionAttribute.cs`), añádelo ahí siguiendo la convención
  documentada (Create/Read/Update/Delete + Approve/Export/Manage) en vez
  de inventar una cadena `"Resource:Action"` suelta en el controller.
- Al invalidar permisos de un usuario (grant/revoke/deny), recordar que
  `PermissionService` cachea 5 minutos en Redis
  (`permissions:user:{userId}`); si se requiere invalidación inmediata hay
  que borrar esa clave explícitamente.

## Consecuencias
- El aislamiento multi-tenant depende de `X-Tenant-Id` correcto y de
  `TenantMembershipMiddleware` (#42a) que cruza JWT con tenant resuelto.
- **Multi-empresa (Fase 1 ✅, fases 2–5 documentadas):** un usuario puede acceder a
  varias `Company` vía `UserCompany`, cambiar empresa activa en sesión y
  dar de alta empresas adicionales. **Fases 4–5** (suscripción gestoría N empresas,
  facturación consolidada) requieren OK de producto — ver tabla de fases en Decisión.
- La resolución por subdominio existe pero el frontend usa `X-Tenant-Id`.
- Tests de aislamiento multi-tenant: parcialmente cubiertos (#32); conviene
  smoke manual con dos tenants antes de desplegar cambios en auth.

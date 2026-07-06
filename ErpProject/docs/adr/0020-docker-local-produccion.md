# ADR-0020: Docker Compose — entorno local vs producción

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto

El ERP se despliega como **stack Docker Compose** en la raíz de `ErpProject/`.
Hay dos modos de operación con objetivos distintos:

| Modo | Objetivo | Quién lo usa |
|------|----------|--------------|
| **Local (desarrollo)** | Levantar BBDD, cache, backend, frontend, proxy y telemetría en la máquina del desarrollador con mínima fricción | Onboarding dev, pruebas E2E manuales, demos |
| **Producción (servidor)** | Exponer la app vía Nginx con TLS, claves Stripe live, sin credenciales de demo ni puertos de depuración | VPS / servidor real (cuando se retome; ver ADR-0003) |

Docker Compose fusiona archivos YAML. Por convención del proyecto:

- **`docker-compose.yml`** — manifiesto base compartido (6 servicios, red, volúmenes).
- **`docker-compose.override.yml`** — ajustes de **desarrollo local**. Compose lo carga **automáticamente** junto al base cuando ejecutas `docker compose up` sin flags `-f`. No hace falta acordarse de un tercer archivo ni copiar comandos largos.
- **`docker-compose.prod.yml`** — override de **producción**. Debe pasarse **explícitamente** con `-f` para evitar mezclar puertos de depuración, seed de admin o `ASPNETCORE_ENVIRONMENT=Development` en un servidor real.

`docker-compose.local.yml` queda **deprecado**; solo re-exporta `docker-compose.override.yml` por compatibilidad con scripts antiguos.

> **Relación con ADR-0003:** el ADR-0003 documenta el despliegue completo (Dockerfiles, scripts `deploy/`, k8s, CI/CD, nginx, backups). **Este ADR (0020)** es la guía operativa día a día para **local vs producción con Compose**: comandos, URLs, migraciones, seed, variables `.env` y troubleshooting. No duplica la arquitectura de infraestructura global; enlaza a ADR-0003 cuando el tema es VPS, Kubernetes o pipeline.

**Nota sobre `docker-compose.override.yml`:** el fichero está **versionado en el repo** (`ErpProject/docker-compose.override.yml`). Tras clonar, `docker compose up` lo carga automáticamente junto al manifiesto base; no hace falta copiarlo ni crearlo a mano. Si el fichero no existiera, el seed dependería solo de `.env` (`Seed__AdminPassword`) y `appsettings.Development.json`.

## Decisión

### Arquitectura Compose: base + override local automático + prod explícito

```
                    ┌─────────────────────────────────────┐
                    │         docker-compose.yml          │
                    │  postgres · redis · backend ·       │
                    │  frontend · nginx · otel-collector  │
                    └─────────────────────────────────────┘
                                      │
              ┌───────────────────────┴───────────────────────┐
              ▼                                               ▼
   docker-compose.override.yml                    docker-compose.prod.yml
   (carga AUTOMÁTICA)                             (flag -f EXPLÍCITO)
   Development · puertos 8081/3000               Production · Stripe live
   seed admin · nginx sin SSL                      nginx SSL · sin seed
              │                                               │
              ▼                                               ▼
   docker compose up -d --build                  docker compose -f docker-compose.yml \
                                                  -f docker-compose.prod.yml up -d --build
```

**Principios:**

1. Un solo `docker-compose.yml` evita divergencia entre entornos en servicios compartidos (imágenes, healthchecks, red `erp-network`, volúmenes `pgdata`/`redisdata`/`uploads`).
2. El override local es opt-out implícito en prod: `docker-compose.prod.yml` no incluye el override de dev; Compose no fusiona `override.yml` cuando solo pasas `-f` explícitos (base + prod).
3. Secretos y URLs sensibles viven en `.env` (nunca en git); la connection string de Postgres se **construye** en `docker-compose.yml` a partir de `POSTGRES_*` para no duplicar contraseñas.

### Override local (`docker-compose.override.yml`)

Contenido versionado en repo (`docker-compose.override.yml`):

```yaml
services:
  backend:
    ports:
      - "8081:8080"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      Seed__AdminPassword: DevChangeMe2026!!
      Postgres__RlsEnabled: "true"
      OpenTelemetry__OtlpEndpoint: http://otel-collector:4317
    volumes:
      - uploads:/app/uploads
      - ./deploy/certs:/app/certs:ro

  frontend:
    ports:
      - "3000:3000"

  nginx:
    ports: !override
      - "80:80"
    volumes: !override
      - ./deploy/nginx/erp.local.conf:/etc/nginx/conf.d/default.conf:ro
      - uploads:/app/uploads:ro
```

## Local — onboarding del desarrollador

Desde la raíz `ErpProject/`:

```bash
# 1. Variables de entorno
cp .env.example .env
# Obligatorio: rellenar POSTGRES_PASSWORD y JWT_SECRET
#   openssl rand -base64 24   → POSTGRES_PASSWORD
#   openssl rand -base64 64   → JWT_SECRET (mínimo 64 caracteres)

# 2. Levantar stack (override se carga solo)
docker compose up -d --build

# 3. Verificar arranque
docker compose ps
docker logs erpproject-backend-1 2>&1 | tail -20
```

Tras el primer arranque exitoso el backend habrá aplicado migraciones EF y, en Development con BD vacía, ejecutado el seed (ver secciones siguientes).

## Servicios del stack

| Servicio | Imagen / build | Función |
|----------|----------------|---------|
| **postgres** | `postgres:16-alpine` | BBDD principal PostgreSQL. Monta `./deploy/postgres` en `/docker-entrypoint-initdb.d` (ejecuta `init-schemas.sql` en el **primer** arranque del volumen). Expone `5432:5432`. Healthcheck `pg_isready`. |
| **redis** | `redis:7-alpine` | Cache y soporte Hangfire (`--appendonly yes`, volumen `redisdata`). Solo red interna. |
| **backend** | `backend/Dockerfile` (.NET 10) | API ASP.NET Core en puerto **8080** interno. Migraciones + seed al arrancar (`Program.cs`). Depende de postgres/redis healthy. |
| **frontend** | `frontend/Dockerfile` (Next.js 15 standalone) | UI en puerto **3000** interno. Variables `API_URL` (red Docker) y `NEXT_PUBLIC_API_URL` (navegador). |
| **nginx** | `nginx:alpine` | Reverse proxy: `/` → frontend, `/api/` → backend, `/swagger` → backend, rate limit en upload QR. Local: `erp.local.conf` sin SSL. Prod: `erp.conf` + certificados en `deploy/nginx/ssl`. |
| **otel-collector** | `otel/opentelemetry-collector-contrib:0.96.0` | Recibe trazas OTLP del backend (`4317` gRPC, `4318` HTTP). Config en `deploy/otel/otel-collector-config.yaml` (export `debug` en local). |

Red compartida: `erp-network` (bridge). Volúmenes persistentes: `pgdata`, `redisdata`, `uploads`.

## URLs de acceso

| Destino | Local | Producción |
|---------|-------|------------|
| Frontend vía Nginx | http://localhost | https://`<DOMAIN>` (puerto 443; certificados en `deploy/nginx/ssl`) |
| Frontend directo | http://localhost:3000 | No expuesto (solo vía nginx) |
| Backend / Swagger | http://localhost:8081/swagger | Solo vía nginx (`/swagger` o `/api/`) |
| API desde el navegador | `NEXT_PUBLIC_API_URL` → `http://localhost:8081` | URL pública del dominio |
| API server-side (Next) | `http://backend:8080` (red Docker) | Igual (nombre de servicio) |
| Postgres (host) | `localhost:5432` | No debería exponerse a internet (ver ADR-0003) |
| OTLP | `localhost:4317` / `:4318` | Opcional según observabilidad en prod |

Healthchecks del backend: `GET /health/live` y `/health/ready` en puerto 8080 (definidos en `backend/Dockerfile`).

## Migraciones de base de datos

Al arrancar, **`Program.cs`** (`ErpProject/backend/Erp.Api/Program.cs`, bloque «Auto-Migrate and Seed») aplica `MigrateAsync()` en **todos** los DbContexts, salvo entorno `IntegrationTests`:

| # | DbContext | Esquema PG | Migraciones (aprox.) |
|---|-----------|------------|----------------------|
| 1 | `ErpDbContext` | `public` (core) | 21 |
| 2 | `BillingDbContext` | `billing` | 8 |
| 3 | `CrmDbContext` | `crm` | 4 |
| 4 | `InventoryDbContext` | `inventory` | 2 |
| 5 | `AccountingDbContext` | `accounting` | 5 |
| 6 | `ExpensesDbContext` | `expenses` | 1 |
| 7 | `TreasuryDbContext` | `treasury` | 2 |
| 8 | `PayrollDbContext` | `payroll` | 3 |
| 9 | `PurchasingDbContext` | `purchasing` | 1 |
| 10 | `SalesDbContext` | `sales` | 6 |
| | **Total** | | **53** |

**Comportamiento:**

- **Idempotente:** `MigrateAsync()` omite migraciones ya aplicadas (`__EFMigrationsHistory` por esquema).
- **Fail-fast:** cualquier excepción en migración o seed provoca `Log.Fatal` y **aborta el arranque** del backend (no queda API sirviendo con esquema a medias).
- **Sin paso manual en deploy:** no hace falta `dotnet ef database update`; el contenedor backend es quien migra.
- **Post-migración RLS:** si `Postgres:RlsEnabled=true`, ejecuta `PostgresRlsBootstrap.ApplyPilotPoliciesAsync` tras las migraciones.

### `init-schemas.sql`

Fichero: `ErpProject/deploy/postgres/init-schemas.sql`. Se ejecuta **una sola vez** cuando el volumen `pgdata` está vacío (primer `docker compose up` de Postgres):

```sql
CREATE SCHEMA IF NOT EXISTS billing;
CREATE SCHEMA IF NOT EXISTS crm;
CREATE SCHEMA IF NOT EXISTS inventory;
CREATE SCHEMA IF NOT EXISTS accounting;
CREATE SCHEMA IF NOT EXISTS expenses;
```

Es **idempotente** (`IF NOT EXISTS`). Los esquemas de módulos más recientes (`treasury`, `payroll`, `purchasing`, `sales`) los crean las propias migraciones EF vía `HasDefaultSchema`. Para BBDD legacy en `public`, existe además `deploy/postgres/migrate-schemas.sql` (manual, ver ADR-0003).

## Seed de datos de desarrollo

**Cuándo corre:** en **cualquier entorno** (no solo Development) si se cumplen ambas condiciones:

1. No hay usuarios en BD: `dbContext.Users.IgnoreQueryFilters().Any()` es `false`.
2. Está configurado `Seed:AdminPassword` (env `Seed__AdminPassword` o `appsettings.Development.json`).

En **local**, `docker-compose.override.yml` fuerza `Seed__AdminPassword=DevChangeMe2026!!`. En **producción** (`docker-compose.prod.yml`) **no** se inyecta seed; el primer usuario debe crearse vía `/signup` o definiendo `Seed__AdminPassword` en `.env` **antes** del primer arranque si se desea bootstrap controlado.

**Por qué `IgnoreQueryFilters`:** en arranque no hay contexto de tenant; los global query filters harían que `Users.Any()` devolviera siempre `false` aunque existieran filas.

**Credenciales demo (solo dev):**

| Campo | Valor |
|-------|-------|
| Email | `admin@devcorp.com` |
| Contraseña | `DevChangeMe2026!!` |
| Empresa | DevCorp S.A. (`B12345678`) |

**Qué inserta el seed (idempotente):**

- Company, roles Admin/Manager/Contable, usuario admin (solo si no existe `admin@devcorp.com`).
- Plan General Contable mínimo (PGC 2007) en `AccountingDbContext` — omite cuentas ya existentes por código.
- `TenantModules` — omite módulos ya registrados; en dev todos quedan `IsEnabled = true`.

Log esperado: `Seed completado: Company, 3 Roles, Admin user, PGC (...), TenantModules.`

## Producción

Comando explícito (sin override de desarrollo):

```bash
cd ErpProject
cp .env.example .env   # rellenar secretos de producción
docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build
```

**Diferencias clave respecto a local:**

| Aspecto | Producción |
|---------|------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` |
| Stripe | Claves **live** obligatorias en `.env` (`Stripe__SecretKey=sk_live_...`, etc.). Validadas por `StripeOptionsValidator` (prefijo `sk_live_` / `pk_live_`). |
| Seed admin | No inyectado por compose; usar `/signup` o `Seed__AdminPassword` explícito en `.env` |
| Puertos backend/frontend | No expuestos al host; solo nginx `:80`/`:443` |
| Nginx | `deploy/nginx/erp.conf` + volumen `deploy/nginx/ssl` (Let's Encrypt) |
| Certificados SII | Ruta servidor `/opt/erp/certs` montada en backend (base `docker-compose.yml`) |
| RLS piloto | `Postgres__RlsEnabled` por defecto `true` (`docker-compose.prod.yml`, `appsettings.json`, `k8s/deployment.yaml`; ADR-0018 #70) |
| Migraciones | Igual que local: automáticas al arrancar backend, fail-fast |

En Development el backend usa `sk_test_...` de `appsettings.Development.json` si no se sobreescribe en env.

## Variables `.env`

Copiar desde `.env.example`. **Nunca** commitear `.env`.

### Obligatorias (local y producción)

| Variable | Descripción |
|----------|-------------|
| `POSTGRES_USER` | Usuario PostgreSQL (default `erp_admin`) |
| `POSTGRES_PASSWORD` | Contraseña BBDD — **obligatoria** |
| `POSTGRES_DB` | Nombre BD (default `erp_saas_db`) |
| `JWT_SECRET` | Secreto JWT — **mínimo 64 caracteres** |
| `JWT_ISSUER` / `JWT_AUDIENCE` | Emisor y audiencia JWT |
| `REDIS_URL` | En Compose: `redis:6379` |
| `ASPNETCORE_URLS` | `http://+:8080` (alineado con Dockerfile/healthcheck) |
| `API_URL` | URL interna Docker: `http://backend:8080` |
| `NEXT_PUBLIC_API_URL` | URL que usa el **navegador** — local: `http://localhost:8081`; prod: URL pública |

### Obligatorias adicionales en producción

| Variable | Descripción |
|----------|-------------|
| `Stripe__SecretKey` | `sk_live_...` |
| `Stripe__WebhookSecret` | `whsec_...` |
| `Stripe__PublishableKey` | `pk_live_...` (recomendado) |
| `Stripe__PriceIds__*` | IDs de precios Stripe por plan |
| `DOMAIN` | Dominio real para nginx/TLS |

### Opcionales / por entorno

| Variable | Local | Producción |
|----------|-------|------------|
| `ASPNETCORE_ENVIRONMENT` | `Development` (override fuerza) | `Production` (prod.yml fuerza) |
| `Seed__AdminPassword` | `DevChangeMe2026!!` (override) | Omitir salvo bootstrap intencional |
| `Postgres__RlsEnabled` | `true` (override) | `true` por defecto (`docker-compose.prod.yml`: `${Postgres__RlsEnabled:-true}`; `appsettings.json` y `k8s/deployment.yaml` también `true`, ADR-0018 #70) |
| `SII_CERT_PATH` / `SII_CERT_PASS` | Opcional; cert en `./deploy/certs` | `/app/certs/fnmt.pfx` en servidor |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | `http://otel-collector:4317` | Según stack observabilidad |
| `BACKUP_RETENTION_DAYS` | 30 (cron backup en servidor) | Igual |
| Stripe en `.env` | Comentado; usa `sk_test` de appsettings | **Requerido** live |

La connection string **no** va en `.env`: Compose la arma como `ConnectionStrings__DefaultConnection` en `docker-compose.yml`.

## Troubleshooting

### Reset completo de base de datos

Si `__EFMigrationsHistory` queda inconsistente (`column already exists`, backend en crash-loop tras migración a medias):

```bash
cd ErpProject
docker compose down
docker volume rm erpproject_pgdata    # nombre puede variar: docker volume ls | grep pgdata
docker compose up -d --build
```

Solo borra datos Postgres; `redisdata` y `uploads` se conservan salvo que se eliminen explícitamente.

Verificación:

```bash
docker logs erpproject-backend-1 2>&1 | grep -E "All module migrations|Seed completado"
```

### Backend en crash-loop por seed o migraciones

- Revisar logs: `docker logs erpproject-backend-1`.
- Si el error es migración: reset de volumen (arriba).
- Si «Database has no users but Seed:AdminPassword is not configured»: añadir `Seed__AdminPassword` al override o `.env`, o registrar usuario en `/signup`.
- Si Stripe en Production sin `sk_live_`: el validador aborta al arrancar — configurar claves en `.env` antes de prod.

### Docker Desktop (Windows / macOS)

- Activar integración WSL2 / recursos suficientes (≥ 4 GB RAM para build .NET + Next).
- Si `docker compose up --build` falla en red: reintentar; builds multi-stage son pesados.
- Puertos ocupados: comprobar que `80`, `3000`, `5432`, `8081` estén libres.
- Si Postgres no pasa healthcheck: esperar 30–60 s en primer arranque; revisar `POSTGRES_PASSWORD` en `.env`.

### Override local no se aplica

Comprobar que existe `docker-compose.override.yml` en la raíz de `ErpProject/` y que no se está usando solo `-f docker-compose.prod.yml` (prod no carga el override de dev). Alternativa explícita: `docker compose -f docker-compose.yml -f docker-compose.local.yml up -d` (alias deprecado que incluye el mismo override).

## Relación con otros ADR

| ADR | Relación |
|-----|----------|
| [0003](0003-despliegue-infraestructura.md) | Infraestructura global: Dockerfiles, scripts VPS, k8s, CI/CD, nginx prod, backups |
| [0002](0002-multitenancy-auth.md) | JWT, multi-tenant, filtros globales (relevante para seed con `IgnoreQueryFilters`) |
| [0014](0014-suscripciones-licencias.md) | Stripe; claves test vs live según entorno |
| [0018](0018-calidad-arquitectura.md) | RLS piloto (#34), OpenTelemetry (#36), validación Stripe (#0e) |

## Evaluación de calidad arquitectónica

> Metodología completa en [ADR-0018](0018-calidad-arquitectura.md). Checklist para **esta pieza** (Compose + arranque):

- **SOLID / DIP:** la orquestación no mezcla lógica de negocio; migraciones y seed viven en `Program.cs` (arranque), no en controllers. Stripe validado vía `IValidateOptions<StripeOptions>`.
- **Clean Architecture:** Compose solo cablea infraestructura; dominio no conoce Docker.
- **Duplicación:** connection string única construida en compose (evita `DATABASE_URL` duplicada en `.env`). Override local centralizado; prod.yml solo sobrescribe lo necesario.
- **Controllers delgados:** N/A para Compose; migraciones no bypassan MediatR en runtime HTTP. `PublicApiController` y `ReportsController` enlazan queries MediatR desde parámetros HTTP (sin `DateTime.Parse` inline).
- **Escalabilidad:** un backend por compose file; escala horizontal documentada en ADR-0003 (k8s), no en este stack local.
- **Riesgos conocidos:** Postgres expuesto en `:5432` en local (aceptable dev); seed con contraseña débil solo en override local; no desplegar `docker-compose.override.yml` en servidores de producción (usar `-f docker-compose.prod.yml` explícito).

## Buenas prácticas aplicables

- Siempre `cp .env.example .env` y generar secretos con `openssl rand`.
- Local: `docker compose up -d --build` (sin `-f` extra).
- Producción: **siempre** `-f docker-compose.yml -f docker-compose.prod.yml`; nunca subir override de dev al servidor.
- Tras cambiar migraciones EF, reiniciar backend (`docker compose restart backend`) o rebuild.
- No commitear `.env`, certificados `.pfx` ni `deploy/nginx/ssl` con claves reales.

## Consecuencias

**Ventajas:**

- Onboarding en tres comandos (`cp`, editar `.env`, `compose up`).
- Override automático reduce errores «olvidé el flag local».
- Migraciones fail-fast evitan API con esquema inconsistente.
- Separación clara test Stripe (Development) vs live (Production).

**Inconvenientes / deuda:**

- `docker-compose.override.yml` versionado: cambios locales deben commitearse para que el equipo comparta la misma config dev (no usar overrides personales sin consenso).
- `init-schemas.sql` solo precarga 5 esquemas; el resto depende de EF (funciona, pero documentación histórica puede confundir).
- En Windows, rutas de volumen SII (`./deploy/certs` vs `/opt/erp/certs`) requieren crear carpetas manualmente.
- Producción en VPS documentada en ADR-0003 pero no es el foco operativo actual del equipo (local first).
- 53 migraciones al arranque alargan el primer boot del backend (aceptable en dev; en prod planificar ventana de deploy).

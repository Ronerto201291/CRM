# ADR-0003: Despliegue e infraestructura

## Estado
Aceptado — refleja la implementación actual del código en `main`.

## Contexto
El README describe un despliegue objetivo en un VPS Hetzner mediante
Docker Compose + Nginx + Let's Encrypt. El repositorio contiene, además,
un manifiesto de Kubernetes (`k8s/`) y un pipeline de GitHub Actions.
Esta ADR documenta, a partir del código de infraestructura real
(`docker-compose*.yml`, `backend/Dockerfile`, `frontend/Dockerfile`,
`deploy/`, `k8s/`, `.github/workflows/ci-cd.yml`), cómo se construye,
despliega y respalda el sistema, y señala las inconsistencias detectadas
entre las distintas piezas.

> **Actualización de contexto**: el VPS sobre el que se diseñó originalmente
> este despliegue ya no existe. Hasta nuevo aviso, el objetivo real es
> **Docker Compose en local con base de datos también en local**
> (`docker-compose.yml` + `docker-compose.override.yml` cargado automáticamente,
> sin `deploy/setup-vps.sh` ni el nginx de producción); el despliegue en servidor se retoma más
> adelante. Todo lo que sigue en esta ADR describe el diseño "as-built" tal
> como está en el repo (incluye la parte de VPS, que se mantiene documentada
> para cuando se retome), pero el trabajo activo debe centrarse en que el
> camino local funcione de punta a punta (ver ADR-0018, ítem 65).

> **Guía operativa local/prod:** para comandos de onboarding, URLs, migraciones
> automáticas, seed de admin, variables `.env` y troubleshooting de Compose,
> ver **[ADR-0020 — Docker Compose local vs producción](0020-docker-local-produccion.md)**.
> Este ADR-0003 sigue siendo la referencia de Dockerfiles, scripts `deploy/`,
> Kubernetes, CI/CD y decisiones de infraestructura global.

## Decisión

### Backend (contenedor)
`backend/Dockerfile` es un build multi-stage:
1. **Build**: imagen `mcr.microsoft.com/dotnet/sdk:10.0`, copia los cuatro
   `.csproj` núcleo (`Erp.Api`, `Erp.Application`, `Erp.Domain`,
   `Erp.Infrastructure`), restaura, copia el resto de `backend/` (incluye
   los `Modules/*`) y publica `Erp.Api.csproj` en modo `Release`.
2. **Runtime**: imagen `mcr.microsoft.com/dotnet/aspnet:10.0`, instala
   `tesseract-ocr` + paquetes de idioma español/inglés (necesarios para el
   Smart Expense Capture / OCR local), crea un usuario no-root
   (`appuser`/`appgroup`, uid/gid 1001) y ejecuta como ese usuario.
   Expone el puerto 8080 (`ASPNETCORE_HTTP_PORTS=8080`) con un
   `HEALTHCHECK` que llama a `http://localhost:8080/health/live` cada 30s.

### Frontend (contenedor)
`frontend/Dockerfile` sigue el patrón estándar de Next.js "standalone":
imagen `node:20-alpine`, etapa `deps` (`npm ci`), etapa `builder`
(`npm run build`), y etapa `runner` que copia `.next/standalone` y
`.next/static`, corre como usuario no-root (`nextjs`, uid 1001) y expone
el puerto 3000.

### Orquestación local/producción: Docker Compose
`docker-compose.yml` (raíz del repo) define cinco servicios en la red
`erp-network`:
- **postgres** (`postgres:16-alpine`): monta `./deploy/postgres` en
  `/docker-entrypoint-initdb.d` — así `init-schemas.sql` se ejecuta
  automáticamente en el primer arranque del contenedor. Healthcheck con
  `pg_isready`.
- **redis** (`redis:7-alpine`), con AOF (`--appendonly yes`) y healthcheck
  `redis-cli ping`.
- **backend**: construido desde `backend/Dockerfile`, variables de entorno
  inyectadas desde `.env` (`DATABASE_URL`, `REDIS_URL`, `JWT_SECRET`,
  `JWT_ISSUER`, `JWT_AUDIENCE`, `SII_CERT_PATH`, `SII_CERT_PASS`, etc.),
  monta un volumen `uploads` y `/opt/erp/certs` (certificados SII/
  VeriFactu) de solo lectura. Depende de que `postgres` y `redis` estén
  `service_healthy`.
- **frontend**: construido desde `frontend/Dockerfile`, variables
  `API_URL`/`NEXT_PUBLIC_API_URL`.
- **nginx** (`nginx:alpine`): expone 80/443, monta
  `deploy/nginx/erp.conf` y el volumen `uploads` (solo lectura, para servir
  ficheros subidos directamente si aplica). Depende de `backend` y
  `frontend`.

`docker-compose.override.yml` es el *override* de desarrollo local (carga
automática con `docker compose up -d`, sin flags `-f`):
expone el backend directamente en `8081:8080` (Swagger sin pasar por
Nginx), fuerza `ASPNETCORE_ENVIRONMENT=Development`, inyecta
`Seed__AdminPassword=DevChangeMe2026!!` (admin semilla `admin@devcorp.com`
si la BD no tiene usuarios) y sustituye la config de Nginx por
`deploy/nginx/erp.local.conf` (sin SSL, sin el volumen de certificados).

`docker-compose.prod.yml` es el override explícito de producción (uso:
`docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d`):
fuerza `ASPNETCORE_ENVIRONMENT=Production`, inyecta claves Stripe live
desde `.env`, mantiene Nginx con SSL (`erp.conf` + `deploy/nginx/ssl`) y
no expone puertos directos de backend/frontend. **No** carga
`docker-compose.override.yml`.

`docker-compose.local.yml` queda como alias deprecado que incluye
`docker-compose.override.yml` por compatibilidad con scripts antiguos.

### Scripts de operación (`deploy/`)
- `deploy/setup-vps.sh`: aprovisionamiento inicial de un VPS Ubuntu 22.04
  (Hetzner): instala Docker + Compose plugin, Nginx, Certbot
  (Let's Encrypt), Tesseract OCR, configura `ufw` (22/80/443) y crea
  `/opt/erp/{backups,uploads}`.
- `deploy/deploy.sh`: `git pull origin main` en `/opt/erp`, reconstruye
  (`docker compose build --no-cache`) y levanta todos los servicios
  (`docker compose up -d`), espera 20s y comprueba salud vía
  `/health/live` y `/health/ready` del backend y la home del frontend.
  Las migraciones EF Core se aplican automáticamente al arrancar el
  backend (`MigrateAsync()` en `Program.cs` para el core y cada módulo),
  en **todos los entornos excepto `IntegrationTests`**. Si alguna falla, el
  proceso aborta el arranque (no continúa con esquema a medias). Las
  migraciones manuales deben llevar `[DbContext]` + `[Migration("…")]` como
  las de Sales; sin ello EF no las descubre y quedan pendientes para
  siempre. No es un paso separado del script de deploy.
- `deploy/backup.sh`: pensado para cron (`0 3 * * *`); lee credenciales de
  `.env`, ejecuta `pg_dump` dentro del contenedor `postgres` vía
  `docker compose exec`, comprime con `gzip` y purga backups más antiguos
  que `BACKUP_RETENTION_DAYS` (por defecto 30 días).
- `deploy/nginx/erp.conf` (producción) y `erp.local.conf` (local): Nginx
  como reverse proxy — `/` y `/api/proxy/` hacia `frontend:3000`, `/api/`
  hacia `backend:8080` (con `client_max_body_size 10M`),
  `/api/expenses/upload/` con `limit_req` (5 req/min) para el endpoint
  público de subida de tickets del QR, `/swagger` también proxied.
  `erp.conf` incluye cabeceras de seguridad (`X-Frame-Options`,
  `X-Content-Type-Options`, HSTS, `Referrer-Policy`) y referencias
  (comentadas) a certificados Let's Encrypt.

### Base de datos: esquemas por módulo
`deploy/postgres/init-schemas.sql` crea, en el primer arranque del
contenedor Postgres, los esquemas: `billing`, `crm`, `inventory`,
`accounting`, `expenses`. **No incluye** `payroll`, `purchasing`, `sales`
ni `treasury`, pese a que esos cuatro módulos también fijan su propio
`HasDefaultSchema(...)` en su `DbContext` (verificado:
`grep HasDefaultSchema backend/Modules/*/Infrastructure/Data/*.cs`
devuelve los nueve nombres). Esto no rompe el arranque porque las
migraciones EF Core de cada módulo generan su propio
`EnsureSchema(...)` al aplicarse (confirmado, p. ej., en la migración
inicial de Payroll), pero el script SQL de referencia ha quedado
desactualizado respecto a los módulos existentes.

`deploy/postgres/migrate-schemas.sql` es un script de migración
**manual, para ejecutar una sola vez** sobre bases de datos existentes que
tuvieran las tablas de módulos aún en el esquema `public` (versión previa
a la extracción física de esquemas): mueve tablas de Billing/CRM/
Inventory/Accounting/Expenses con `ALTER TABLE ... SET SCHEMA ...` y deja
instrucciones para marcar manualmente las migraciones `InitialCreate` de
cada módulo como ya aplicadas en `__EFMigrationsHistory`. No cubre
Payroll/Purchasing/Sales/Treasury (coherente con que esos módulos
probablemente nacieron ya con esquema propio, sin datos previos en
`public` que migrar).

### Kubernetes (`k8s/deployment.yaml`)
Existe un manifiesto alternativo con `Deployment` + `Service` para
`erp-api` (2 réplicas, `readinessProbe` en `/health`, límites de
recursos 256Mi/250m→512Mi/500m) y `erp-frontend` (2 réplicas, 128Mi/100m→
256Mi/250m), más un `Ingress` que enruta `/api` → `erp-api` y `/` →
`erp-frontend`. **Este manifiesto no despliega Postgres ni Redis** (asume
que `erp-redis:6379` y el secreto `erp-secrets` ya existen en el clúster),
ni contempla Hangfire, Nginx, ni el volumen de `uploads`/certificados SII
que sí gestiona Docker Compose. No hay evidencia en el repositorio de que
esta ruta de despliegue esté realmente en uso (no aparece referenciada
desde `deploy/*.sh` ni desde el pipeline de CI/CD).

### CI/CD
**Existen dos workflows de GitHub Actions con contenido distinto**, en
rutas diferentes del repositorio:
- `/home/user/CRM/.github/workflows/ci-cd.yml` (raíz del repo): jobs
  `build-and-test` → `docker-build` → `deploy`. Restaura/compila solo
  `ErpProject/backend/Erp.Api/Erp.Api.csproj` (no el `.slnx` completo);
  el paso "Run tests" comprueba dinámicamente si existe algún
  `*.Tests.csproj` bajo `ErpProject/backend` y, si no lo hay (que es el
  caso actual — ver ADR-0001), lo salta con el mensaje
  *"No test projects found — skipping."* en vez de fallar. Construye y
  publica imágenes a `ghcr.io/<owner>/erp-{backend,frontend}` (con
  `docker/build-push-action`, cache de GHA) solo en push a `main`, y
  despliega por SSH a Hetzner ejecutando `docker compose pull` +
  `docker compose up -d --no-deps --remove-orphans backend frontend`
  sobre `/opt/erp`.
- `/home/user/CRM/ErpProject/.github/workflows/ci-cd.yml`: pipeline
  distinto (`ERP CI/CD Pipeline`), dispara también en `develop`, compila
  contra `backend/Erp.slnx` completo, **ejecuta `dotnet test` de forma
  incondicional** (`dotnet test backend/Erp.slnx --no-build -c Release`,
  sin el guard de "si no hay proyectos de test") — lo que fallaría o no
  haría nada útil dado que no hay ningún `*.Tests.csproj` referenciado
  desde `Erp.slnx`. Tiene un job aparte para el frontend
  (`npm ci && npm run build`), construye imágenes Docker locales
  (`docker build -t erp-api:$SHA ...`) sin publicarlas a ningún registro,
  y despliega vía la acción `appleboy/ssh-action` haciendo `git pull` +
  `docker compose pull && docker compose up -d --remove-orphans`
  directamente en el servidor (sin especificar servicios, afectando
  también a `postgres`/`redis`/`nginx`).

  Solo uno de estos workflows puede ser el que realmente ejecuta GitHub
  Actions, dependiendo de cuál sea la raíz real del repositorio en GitHub
  (si el repo remoto tiene `ErpProject/` como subcarpeta, ambos podrían
  incluso dispararse a la vez sobre el mismo push). Esta duplicación con
  contenido divergente es una inconsistencia real del repositorio que
  conviene resolver: decidir cuál es el pipeline "fuente de la verdad" y
  eliminar o archivar el otro.

## Relación con otros módulos
Esta infraestructura es transversal a todos los módulos: la imagen única
`backend` aloja el monolito modular completo (ADR-0001), por lo que
cualquier cambio en cualquier módulo de negocio requiere reconstruir y
redesplegar la misma imagen Docker. Postgres aloja los esquemas de todos
los módulos en la misma instancia (ver ADR-0001, sección "Modelo de
datos"). El volumen `/opt/erp/certs` montado en el backend se usa para los
certificados de firma electrónica de SII/VeriFactu (ADR-0013).

## Evaluación de calidad arquitectónica
> Metodología completa y hallazgos transversales en `ADR-0018`.

Auditoría dedicada de infraestructura (ADR-0018 §"Ítems 52-64") encontró
varios **bugs confirmados**, no solo deuda de diseño: el health-check
post-deploy (`deploy/deploy.sh` y job `deploy` en `ci-cd.yml`) apuntaba al
puerto 5000 pero el backend escucha en **8080** (`ASPNETCORE_URLS` /
`backend/Dockerfile`) — **✅ Corregido (jul 2026)**: ambos scripts usan
`http://localhost:8080/health/live`; el job CI falla explícitamente si el
healthcheck no responde. El backend en `docker-compose.yml` sigue sin
publicar puertos en producción (solo nginx), por lo que el curl en el
servidor asume acceso desde el host al contenedor backend vía red Docker
interna o proxy — coherente con el diseño nginx-only. Otros ítems abiertos:
TLS desactivado en `deploy/nginx/erp.conf` (ítem 53); nginx duplicado SO vs
contenedor (ítem 54); Postgres 5432 expuesto en compose base (ítem 55);
imágenes CI vs deploy manual (ítem 59).

## Buenas prácticas aplicables
- Cualquier módulo nuevo que añada un esquema Postgres propio debería
  añadirse también a `deploy/postgres/init-schemas.sql` para mantener ese
  script como referencia fiable, aunque no sea estrictamente necesario
  para el arranque (las migraciones EF ya crean el esquema).
- Antes de tocar el pipeline de CI/CD, confirmar cuál de los dos
  `ci-cd.yml` es el que efectivamente ejecuta GitHub Actions en el
  repositorio remoto, y consolidar en uno solo.
- Si se retoma la vía Kubernetes, completar el manifiesto con Postgres,
  Redis, Hangfire y el volumen de `uploads`/certificados antes de
  considerarlo una alternativa real a Docker Compose.
- Los scripts de `deploy/` asumen la ruta fija `/opt/erp` y un `.env` con
  las variables citadas (`POSTGRES_DB`, `POSTGRES_USER`,
  `BACKUP_RETENTION_DAYS`, `DATABASE_URL`, `REDIS_URL`, `JWT_*`,
  `SII_CERT_*`, `API_URL`, `NEXT_PUBLIC_API_URL`); cualquier cambio de
  convención de despliegue debe mantener esa ruta o actualizar los tres
  scripts a la vez.

## Consecuencias
- **Dos rutas de despliegue en paralelo** (Docker Compose en un VPS
  Hetzner vs. manifiesto de Kubernetes) sin que quede claro en el
  repositorio cuál es la de producción real; el manifiesto de k8s está
  incompleto respecto a las dependencias que Docker Compose sí modela
  (Postgres, Redis, Hangfire, volúmenes). Merece la pena documentar
  explícitamente cuál es la vía soportada, o retirar la que no se usa.
- **Dos workflows de CI/CD divergentes** (`/.github/workflows/ci-cd.yml` y
  `/ErpProject/.github/workflows/ci-cd.yml`) con distinta estrategia de
  build, distinto manejo de tests, y distinto mecanismo de despliegue.
  Mantenerlos ambos vivos es una fuente de confusión y riesgo (posible
  doble despliegue, o despliegue con la versión equivocada del pipeline).
- El pipeline de la raíz del repo (`/.github/workflows/ci-cd.yml`) es el
  único que salta explícitamente `dotnet test` cuando no hay proyectos de
  test; el otro (`ErpProject/.github/workflows/ci-cd.yml`) ejecuta
  `dotnet test` sin ese guard, lo que probablemente sea un no-op silencioso
  hoy pero fallaría en cuanto se intente ejecutar contra el `.slnx`
  completo si algún día se añaden tests reales sin ajustar el paso.
- Los backups (`deploy/backup.sh`) son solo de Postgres (`pg_dump`); no
  hay backup automatizado del volumen `uploads` (documentos OCR, adjuntos)
  ni de los certificados SII, que quedan fuera del `pg_dump` y merecerían
  una estrategia de respaldo propia si aún no la tienen.
- **Seed de bootstrap**: si la BD no tiene usuarios (`Users.Any()` con
  `IgnoreQueryFilters` — sin tenant en arranque el filtro global daría
  falso positivo), `Program.cs` crea `admin@devcorp.com` solo cuando
  `Seed:AdminPassword` está definido (env `Seed__AdminPassword` o
  `appsettings.Development.json`). Inserciones de PGC, `TenantModules` y
  admin son idempotentes por si un arranque anterior quedó a medias. En
  producción sin seed hay que usar `/signup` o definir la variable antes
  del primer arranque.

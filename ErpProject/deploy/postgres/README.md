# PostgreSQL — piloto RLS (ADR-0018 #34)

Row-Level Security opcional para defensa en profundidad multi-tenant a nivel de base de datos.

## Requisitos

1. **Interceptor** `PostgresTenantSessionInterceptor` (ya en `Erp.Infrastructure`) — ejecuta `SET app.current_tenant = '<uuid>'` en cada conexión Npgsql cuando `Postgres:RlsEnabled=true`.
2. **Script SQL** `rls-pilot.sql` — políticas de ejemplo sobre tablas core.
3. **Variable de entorno** `Postgres__RlsEnabled=true` (ver `.env.example`).

## Reset de BBDD local (migraciones EF inconsistentes)

Si `__EFMigrationsHistory` queda a medias (p. ej. backend en crash-loop con
`column already exists` o `Navigation 'Quote.Lines' was not found`), la vía más
segura en desarrollo es recrear el volumen Postgres:

```bash
cd ErpProject
docker compose down
docker volume rm erpproject_pgdata   # solo datos Postgres; uploads/redis se conservan si no se borran
docker compose up -d --build
```

El backend aplica todas las migraciones (core + 9 módulos) y ejecuta el seed en
Development. Verificar:

```bash
docker exec erpproject-postgres-1 psql -U erp_admin -d erp_saas_db -c \
  'SELECT COUNT(*) FROM public."__EFMigrationsHistory";'
docker logs erpproject-backend-1 2>&1 | grep -E "All module migrations|Seed completado"
```

## Activación local (piloto)

```bash
# 1. Levantar stack local (Postgres__RlsEnabled=true en docker-compose.override.yml)
docker compose up -d --build

# 2. El backend aplica migraciones EF y luego PostgresRlsBootstrap (Companies + 9 tablas CompanyId)
#    Ver log: "RLS pilot: políticas aplicadas"

# 3. Re-aplicar manualmente si hace falta:
psql -h localhost -U erp_admin -d erp_saas_db -f deploy/postgres/rls-pilot.sql
```

## Comportamiento

| `Postgres:RlsEnabled` | Comportamiento |
|----------------------|----------------|
| `false` (default) | Solo filtros EF global query + tenant middleware (comportamiento actual) |
| `true` | Además, Postgres aplica políticas RLS si están habilitadas en tablas |

## Notas

- El rol de migraciones (`erp_migration`) debe tener bypass o `FORCE ROW LEVEL SECURITY` bloqueará migraciones.
- Empezar por tablas core (`Companies`, `Users`, `ApiKeys`, `AuditLogs`, etc. — ver `rls-pilot.sql`).
- No sustituye el filtro de tenant en aplicación; es capa adicional.

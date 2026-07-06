# Manifiestos Kubernetes — DEPRECATED

> **Estado (jul 2026):** estos manifiestos **no se usan en CI/CD ni en producción**.
> El despliegue soportado es **Docker Compose** (`docker-compose.yml` + `docker-compose.prod.yml`).
> Ver [ADR-0003](../docs/adr/0003-despliegue-infraestructura.md) y [ADR-0020](../docs/adr/0020-docker-local-produccion.md).

## Por qué están obsoletos

| Aspecto | Compose (activo) | `k8s/deployment.yaml` (obsoleto) |
|---------|------------------|----------------------------------|
| Postgres, Redis | Incluidos | Asume externos |
| Nginx, otel-collector | 6 servicios | Solo api + frontend |
| Volúmenes uploads/certs SII | Sí | No |
| CI/CD | `.github/workflows/ci-cd.yml` | No referenciado |
| Imágenes | GHCR en prod | `erp-api:latest` local |

## Si quieres retomar k8s

Antes de desplegar, completar:

1. StatefulSet/Helm para Postgres y Redis (o servicios gestionados).
2. PVC para `uploads/` y certificados SII/VeriFactu.
3. ConfigMap/Secret con JWT, Stripe, SII, OTel (como en `.env.example`).
4. Imágenes desde GHCR (`ghcr.io/<org>/erp-api`, `erp-frontend`).
5. `readinessProbe` en `/health/live` (coherente con Dockerfile).
6. Job de migraciones EF o confiar en arranque de `Program.cs` (documentar).

**No usar `deployment.yaml` tal cual** — contradice el stack real y fallará sin dependencias.

# Workflows de CI/CD

El pipeline activo vive en la **raíz del monorepo**:

```
.github/workflows/ci-cd.yml
```

GitHub Actions solo ejecuta workflows bajo `.github/workflows/` en la raíz del repositorio (`c:\CRM`). El archivo `ci-cd.yml` que existía aquí fue consolidado allí (jul 2026) para unificar:

- Backend: build, architecture tests, unit/integration con Coverlet XPlat, `scripts/check-coverage.py`
- Frontend: lint, `npm run test:coverage` (Vitest gates), build
- E2E Playwright con stack Docker
- Docker build & push a GHCR (solo `main`, tras pasar todos los tests)
- Deploy Hetzner (solo `main`)

Ver `ErpProject/docs/testing-strategy.md` para umbrales y comandos locales.

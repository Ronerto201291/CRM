# ERP SaaS España 🇪🇸

> Plataforma SaaS de gestión empresarial para PYMES españolas. Legalmente robusta, preparada para normativa antifraude y futura integración Verifactu.

## 🏗 Stack Tecnológico

| Capa | Tecnología |
|------|-----------|
| **Backend** | .NET 10 · ASP.NET Core · Clean Architecture · CQRS (MediatR) · EF Core |
| **Frontend** | Next.js 15 · TypeScript · Tailwind CSS |
| **Base de datos** | PostgreSQL 16 |
| **Cache** | Redis 7 |
| **Jobs** | Hangfire |
| **OCR** | Tesseract (local) |
| **Deploy** | Docker Compose · Nginx · Let's Encrypt |

## 📦 Módulos

- **🔐 Autenticación** — JWT + Refresh Tokens · Multi-tenant por CompanyId · Roles (Admin/Manager/Contable)
- **👥 CRM** — Clientes · Proveedores · Contactos · Timeline de actividad
- **🧾 Facturación** — Conforme RD 1619/2012 · Ley 11/2021 Antifraude · SHA256 hash chain · IVA 21/10/4/Exento · IRPF · Recargo equivalencia · Facturas rectificativas
- **📊 Contabilidad** — Asientos automáticos · Libro diario · Balance · IVA soportado/repercutido
- **📸 Smart Expense Capture** — QR único por empresa · Upload público · OCR local (Tesseract) · Auto-crear proveedor
- **📦 Inventario** — Productos · Stock · Movimientos entrada/salida

## 🚀 Inicio Rápido (local con Docker)

```bash
# 1. Clonar y preparar entorno
git clone <repo> && cd ErpProject
cp .env.example .env   # rellena POSTGRES_PASSWORD y JWT_SECRET (openssl rand -base64)

# 2. Levantar stack de desarrollo (docker-compose.override.yml se carga solo)
docker compose up -d --build

# 3. Acceder
# Frontend (nginx):  http://localhost
# Frontend (directo): http://localhost:3000
# Backend/Swagger:    http://localhost:8081/swagger
# Login demo:         admin@devcorp.com / DevChangeMe2026!!
```

> **Producción** (VPS/servidor, sin overrides de desarrollo):
> `docker compose -f docker-compose.yml -f docker-compose.prod.yml up -d --build`

Las migraciones EF Core (core + módulos) se aplican **automáticamente** al arrancar el backend; no hace falta ejecutarlas a mano.

## 🧪 Tests y cobertura

```bash
cd ErpProject/backend && dotnet test              # 613 tests backend
cd ErpProject/frontend && npm test                # 123 Vitest (sin cobertura)
cd ErpProject/frontend && npm run test:coverage   # Vitest + gate umbral líneas 39%
```

### Coverage gates (bloquean deploy en CI)

Si la cobertura baja por debajo del umbral, **el pipeline falla** y no se construyen imágenes Docker en `main`.

| Área | Umbral mínimo | Medido (jul 2026) |
|------|---------------|-------------------|
| Backend merged (unit+integration XPlat) | **49%** línea | ~51% |
| Backend unit XPlat | **27%** línea | ~28.2% |
| Backend integration XPlat | **49%** línea | ~51.2% |
| Billing.Application | **55%** | ~59.7% |
| Accounting.Application | **18%** | ~20.1% |
| Erp.Infrastructure (auth) | **50%** | ~55.1% |
| Frontend Vitest (clientes testeados) | **39%** líneas | ~53% |

Umbrales en `scripts/coverage-thresholds.json`. Gate backend: `scripts/check-coverage.py`. Local:

```bash
# Backend (requiere Python 3)
bash ErpProject/scripts/run-backend-coverage-gate.sh

# Frontend
bash ErpProject/scripts/run-frontend-coverage-gate.sh
```

Upload Codecov opcional: secret `CODECOV_TOKEN` en GitHub.

## 🏢 Multi-Tenant

Cada empresa tiene su `CompanyId`. Global query filters en EF Core aseguran aislamiento total de datos. Middleware automático inyecta `CompanyId` en cada request.

## 📱 Smart Expense Capture (QR)

1. Generar QR en **Configuración**
2. Escanear QR con el móvil → subir foto del ticket
3. OCR extrae automáticamente: CIF, nombre, base, IVA, total
4. Revisar en panel → Aprobar → Se genera asiento contable + bloqueo

## 🔒 Normativa Fiscal Española

| Requisito | Implementación |
|-----------|---------------|
| Numeración correlativa | `{Serie}-{Año}-{Secuencia:D6}` |
| Hash SHA256 encadenado | `Hash(Number\|Total\|Date\|PreviousHash)` |
| Bloqueo tras contabilización | Campo `IsLocked` + `LockedAt` |
| IVA español | 21%, 10%, 4%, Exento |
| IRPF profesionales | Campo `IrpfRate` (0% o 15%) |
| Recargo equivalencia | 5.2%, 1.4%, 0.5% |
| AuditLog inmutable | JSONB con OldValues/NewValues |
| Factura rectificativa | `InvoiceType = "Rectificativa"` + FK a original |

## 🖥 Deploy en VPS (Hetzner)

```bash
# Setup inicial
chmod +x deploy/setup-vps.sh && ./deploy/setup-vps.sh

# Deploy
chmod +x deploy/deploy.sh && ./deploy/deploy.sh

# Backup (cron automático)
crontab -e
# 0 3 * * * /opt/erp/deploy/backup.sh
```

## 📁 Estructura del Proyecto

```
ErpProject/
├── backend/
│   ├── Erp.Api/          # Controllers, Program.cs, Middleware
│   ├── Erp.Application/  # CQRS Commands/Queries/Handlers, DTOs
│   ├── Erp.Domain/       # Entities, Common, Value Objects
│   └── Erp.Infrastructure/ # DbContext, Services, Security
├── frontend/
│   └── src/app/          # Next.js App Router pages
├── deploy/               # Nginx, scripts de deploy/backup
├── docker-compose.yml
├── docker-compose.override.yml   # local (carga automática)
├── docker-compose.prod.yml       # producción (explícito con -f)
└── README.md
```

## 👤 Credenciales Demo

| Campo | Valor |
|-------|-------|
| Email | `admin@devcorp.com` |
| Password | `DevChangeMe2026!!` |
| Empresa | DevCorp S.A. |

---

**Licencia**: Propietaria · © 2026 DevCorp

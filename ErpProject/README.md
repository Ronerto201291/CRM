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

## 🚀 Inicio Rápido

```bash
# 1. Clonar y levantar
git clone <repo> && cd ErpProject
docker compose up -d

# 2. Acceder
# Frontend: http://localhost:3000
# Backend API: http://localhost:5000/swagger
# Login: admin@devcorp.com / Roberto21$$
```

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
└── README.md
```

## 👤 Credenciales Demo

| Campo | Valor |
|-------|-------|
| Email | `admin@devcorp.com` |
| Password | `Roberto21$$` |
| Empresa | DevCorp S.A. |

---

**Licencia**: Propietaria · © 2026 DevCorp

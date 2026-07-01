# GitHub Repository Secrets — Guía de Configuración

Guía para configurar los secretos necesarios en GitHub para que el pipeline CI/CD funcione en producción.

---

## Acceso a la configuración de secretos

> **GitHub → Repository → Settings → Secrets and variables → Actions → New repository secret**

URL directa: `https://github.com/<org>/<repo>/settings/secrets/actions`

---

## Secretos requeridos

### 🔐 Infraestructura – Servidor Hetzner (CI/CD Deploy)

| Secret | Descripción | Ejemplo |
|--------|-------------|---------|
| `HETZNER_HOST` | IP pública o hostname del servidor | `65.21.x.x` o `erp.tudominio.com` |
| `HETZNER_USER` | Usuario SSH en el servidor | `erp` |
| `HETZNER_SSH_KEY` | Clave privada SSH (formato PEM completo) | `-----BEGIN OPENSSH PRIVATE KEY-----\n...` |
| `HETZNER_SSH_PORT` | Puerto SSH (omitir si es 22) | `22` |

#### Generar par de claves SSH para el deploy

```bash
# Generar clave específica para el deploy (no reutilizar tu clave personal)
ssh-keygen -t ed25519 -C "github-actions-deploy" -f ~/.ssh/erp_deploy_key

# Añadir la clave PÚBLICA al servidor
ssh-copy-id -i ~/.ssh/erp_deploy_key.pub erp@IP_SERVIDOR
# o manualmente:
cat ~/.ssh/erp_deploy_key.pub >> /home/erp/.ssh/authorized_keys

# El contenido de la clave PRIVADA es lo que va en HETZNER_SSH_KEY:
cat ~/.ssh/erp_deploy_key
```

---

### 🔐 Aplicación – Variables de entorno de producción

| Secret | Descripción | Notas |
|--------|-------------|-------|
| `JWT_SECRET` | Clave de firma JWT (mínimo 32 bytes, Base64) | Ya en `.env`, añadir aquí para CI |
| `POSTGRES_PASSWORD` | Contraseña de PostgreSQL en producción | Ya en `.env` |

> **Nota**: El pipeline actual usa SSH para `docker compose up` con el `.env` ya presente en el servidor. Estos secretos son necesarios si en el futuro se genera el `.env` dinámicamente desde el pipeline (recomendado para mayor seguridad).

#### Generar un JWT_SECRET seguro

```bash
# Genera 64 bytes aleatorios en Base64 (compatible con el JwtProvider actual)
openssl rand -base64 64
```

---

## Configuración del servidor Hetzner para el deploy automático

El job de deploy hace `git pull` en el servidor. El repositorio debe estar clonado en `/opt/erp`:

```bash
# En el servidor (primera vez)
sudo mkdir -p /opt/erp
sudo chown erp:erp /opt/erp
cd /opt/erp

# Clonar el repositorio (usar deploy key o HTTPS con token)
git clone https://github.com/<org>/<repo>.git .

# Verificar que el .env de producción está en su sitio
ls -la /opt/erp/ErpProject/.env
```

### Deploy key de repositorio (alternativa a HTTPS)

Para que el servidor pueda hacer `git pull` sin contraseña:

1. Generar clave SSH en el servidor: `ssh-keygen -t ed25519 -f ~/.ssh/github_deploy`
2. Añadir la clave pública en: **GitHub → Repository → Settings → Deploy keys → Add deploy key** (solo lectura)
3. Configurar SSH en el servidor:
   ```
   # ~/.ssh/config
   Host github.com
     IdentityFile ~/.ssh/github_deploy
   ```

---

## GitHub Environment: `production`

El job de deploy usa `environment: production`. Para añadir protecciones (aprobación manual antes de deploy):

1. **GitHub → Repository → Settings → Environments → production**
2. Activar "Required reviewers" y añadir los aprobadores.
3. Opcionalmente restringir a ramas protegidas (`main` solamente).

---

## Verificar que los secretos están configurados

```bash
# Tras el primer push a main, ir a:
# GitHub → Actions → último workflow → job "deploy" → logs
# Debe mostrar:
# ✅ Connected to HETZNER_HOST
# ✅ Deploy completed at <timestamp>
```

---

*Última actualización: 2026-03-22*

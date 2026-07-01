# Certificado FNMT-RCM y Alta en Verifactu (AEAT)

Guía de operaciones para obtener el certificado de representación y dar de alta a cada tenant en el sistema Verifactu de la AEAT.

---

## 1. Obtención del Certificado FNMT-RCM

### 1.1 Tipo de certificado necesario

Para Verifactu se requiere un **Certificado de Representante de Persona Jurídica** (emitido por FNMT-RCM) para firmar los registros de facturación enviados a la AEAT.

- URL oficial: https://www.sede.fnmt.gob.es/certificados/certificado-de-representante
- Formato de exportación: **`.pfx` / PKCS#12** (contiene clave privada + certificado)
- Validez: 2 años (renovar antes de expiración para evitar corte del servicio)

### 1.2 Pasos para obtener el certificado

1. **Acreditar la identidad** del representante legal ante la AEAT o Notario (presencial).
2. **Solicitar el certificado** en la sede FNMT con el NIF de la empresa y del representante.
3. **Descargar e instalar** el certificado en el navegador (requiere IE/Edge o Firefox con perfil de seguridad bajo).
4. **Exportar como `.pfx`** desde el gestor de certificados del navegador / sistema operativo:
   - Windows: `certmgr.msc` → Personal → Certificados → botón derecho → Exportar → incluir clave privada
   - Establecer una contraseña segura al exportar (se usará como `SII_CERT_PASS`).

---

## 2. Despliegue del Certificado en el Servidor Hetzner

```bash
# En el servidor Hetzner (como root o sudo)
mkdir -p /opt/erp/certs
chmod 700 /opt/erp/certs

# Copiar el .pfx desde tu máquina local (sustituye IP_SERVIDOR)
scp fnmt_empresa.pfx erp@IP_SERVIDOR:/opt/erp/certs/fnmt.pfx

# Restringir permisos (solo lectura por el usuario del proceso)
chmod 400 /opt/erp/certs/fnmt.pfx
```

El `docker-compose.yml` monta `/opt/erp/certs` como volumen de solo lectura en `/app/certs`:

```yaml
volumes:
  - /opt/erp/certs:/app/certs:ro
```

El backend lee la ruta y contraseña del certificado vía variables de entorno (ya configuradas en `.env`):

```env
SII_CERT_PATH=/app/certs/fnmt.pfx
SII_CERT_PASS=<contraseña del .pfx>
```

---

## 3. Alta en Verifactu (AEAT)

### 3.1 ¿Qué es Verifactu?

Sistema de **Verificación de Facturación** de la AEAT (Real Decreto 1007/2023). A partir de 2026, todos los sistemas de facturación de empresas >1M€ de facturación deben enviar registros de factura firmados en tiempo real.

### 3.2 Proceso de alta por tenant

Para cada empresa (tenant) que use la plataforma:

1. **Registrar el software** en el Registro de Sistemas Informáticos de Facturación (RSIEF) de la AEAT:
   - URL: https://www.agenciatributaria.gob.es/AEAT.sede/procedimientoini/GE02.shtml
   - Datos necesarios: NIF del fabricante del software, nombre del producto, versión, NIF del cliente.

2. **Obtener las credenciales de producción** (diferenciadas de las credenciales de prueba):
   - Entorno de pruebas: `https://seudesadministrativa.dehu.minhap.es/` (certificado de pruebas FNMT)
   - Entorno de producción: `https://www1.agenciatributaria.gob.es/wlpl/BUGC-CODI/ValidaFact`

3. **Configurar en la base de datos** (por tenant):
   ```sql
   -- Añadir configuración Verifactu al tenant
   UPDATE "Companies"
   SET "VerifactuEnabled" = true,
       "VerifactuMode" = 'Production'  -- o 'Test'
   WHERE "Id" = '<tenant-id>';
   ```

4. **Primer envío**: Enviar un registro de alta de sistema con los datos del certificado y el NIF del tenant. La AEAT responde con un código de aceptación que debe almacenarse.

### 3.3 Estructura del endpoint SII/Verifactu (backend)

El backend expone el servicio en:
- `POST /api/sii/submit` — Envío de registro de factura firmado
- `GET  /api/sii/status/{invoiceId}` — Consulta de estado de registro

El servicio usa el certificado montado en `/app/certs/fnmt.pfx` para firmar las peticiones XML con SHA-256 + RSA antes de enviarlas a la AEAT.

---

## 4. Renovación del Certificado

La FNMT envía un aviso por email 2 meses antes de la expiración. El proceso de renovación es:

1. Generar solicitud de renovación desde el certificado vigente (sin presencialidad si no ha expirado).
2. Exportar el nuevo `.pfx`.
3. Subir al servidor (mismo procedimiento que sección 2).
4. Reiniciar el backend para que cargue el nuevo certificado:
   ```bash
   cd /opt/erp && docker compose restart backend
   ```

> **⚠️ CRÍTICO**: Si el certificado expira sin renovar, el backend no podrá firmar facturas y los envíos a la AEAT fallarán. Configurar una alerta de calendario 60 días antes de la fecha de expiración.

---

## 5. Multi-tenant: Certificados por Empresa

Si cada tenant usa su propio certificado (en lugar del certificado del integrador):

```bash
# Estructura de certificados por tenant
/opt/erp/certs/
├── fnmt.pfx                    # Certificado del integrador (default)
├── tenant_<id1>/
│   └── fnmt.pfx                # Certificado propio del tenant 1
└── tenant_<id2>/
    └── fnmt.pfx                # Certificado propio del tenant 2
```

El backend resuelve el certificado a usar según el `CompanyId` del contexto del tenant.

---

*Última actualización: 2026-03-22*

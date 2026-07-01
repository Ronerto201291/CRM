# ?? API PÚBLICA v1 - DOCUMENTACIÓN COMPLETA

## ?? Autenticación

Todas las requests a `/api/v1` (excepto `/health`) requieren una API Key en el header:

```bash
X-API-Key: sk_live_XXXXXXXXXXXXXXXXXXXXXXXXXXXXXXXX
```

### Obtener API Key

1. Inicia sesión en el ERP SaaS
2. Ve a Sistema ? ?? API Keys
3. Haz clic en "+ Crear Nueva API Key"
4. Configura nombre y rate limit
5. La clave se muestra **una sola vez** - cópiala y guárdala de forma segura

### Validar API Key

```bash
GET /api/v1/auth/verify
X-API-Key: tu_clave_api
```

Respuesta:
```json
{
  "message": "API Key válida",
  "status": "authenticated"
}
```

---

## ?? BASE URL

```
Producción:  https://api.tudominio.com/api/v1
Desarrollo:  http://localhost:5000/api/v1
```

---

## ?? Health Check

Verifica que el API está funcionando (sin autenticación):

```bash
GET /api/v1/health
```

Respuesta:
```json
{
  "status": "healthy",
  "timestamp": "2025-01-14T10:30:00Z",
  "version": "1.0"
}
```

---

## ?? FACTURAS

### Listar Facturas

```bash
GET /api/v1/invoices?status=Paid&clientId=UUID&page=1&pageSize=50
X-API-Key: sk_live_XXXXX
```

**Query Parameters:**
- `status` (opcional): Draft, Issued, Paid, Locked
- `clientId` (opcional): Filtrar por cliente
- `dateFrom` (opcional): YYYY-MM-DD
- `dateTo` (opcional): YYYY-MM-DD
- `page` (default: 1)
- `pageSize` (default: 50, max: 500)

**Respuesta:**
```json
{
  "items": [
    {
      "id": "uuid",
      "number": "A-2025-001",
      "series": "A",
      "clientName": "Empresa SL",
      "issueDate": "2025-01-14",
      "dueDate": "2025-02-14",
      "subtotal": 1000.00,
      "taxAmount": 210.00,
      "irpfAmount": 0,
      "total": 1210.00,
      "status": "Paid",
      "isLocked": false,
      "lines": [
        {
          "description": "Servicio de consultoría",
          "quantity": 1,
          "unitPrice": 1000.00,
          "taxRate": 21,
          "surchargeRate": 0
        }
      ]
    }
  ],
  "totalCount": 150,
  "pageNumber": 1,
  "pageSize": 50,
  "hasNextPage": true,
  "hasPreviousPage": false
}
```

### Obtener Factura por ID

```bash
GET /api/v1/invoices/{id}
X-API-Key: sk_live_XXXXX
```

Respuesta: (mismo formato que arriba para una factura)

### Crear Factura

```bash
POST /api/v1/invoices
X-API-Key: sk_live_XXXXX
Content-Type: application/json

{
  "clientId": "uuid",
  "series": "A",
  "dueDate": "2025-02-14",
  "irpfRate": 0,
  "invoiceType": "Normal",
  "lines": [
    {
      "description": "Servicio de consultoría",
      "quantity": 1,
      "unitPrice": 1000.00,
      "taxRate": 21,
      "surchargeRate": 0
    }
  ]
}
```

Respuesta (201 Created):
```json
{
  "id": "uuid",
  "number": "A-2025-001",
  "issuedAt": "2025-01-14T10:30:00Z",
  "total": 1210.00
}
```

### Actualizar Factura (Borrador)

```bash
PATCH /api/v1/invoices/{id}
X-API-Key: sk_live_XXXXX
Content-Type: application/json

{
  "clientId": "uuid",
  "dueDate": "2025-02-14",
  "lines": [...]
}
```

**Nota:** Solo se pueden editar facturas en estado Draft

### Marcar Factura como Pagada

```bash
POST /api/v1/invoices/{id}/pay
X-API-Key: sk_live_XXXXX
```

Respuesta:
```json
{
  "message": "Factura marcada como pagada"
}
```

### Bloquear/Contabilizar Factura

```bash
POST /api/v1/invoices/{id}/lock
X-API-Key: sk_live_XXXXX
```

**?? ACCIÓN IRREVERSIBLE** - Genera asientos contables automáticamente

Respuesta:
```json
{
  "message": "Factura bloqueada y contabilizada"
}
```

---

## ?? CLIENTES

### Listar Clientes

```bash
GET /api/v1/clients?search=empresa&page=1&pageSize=50
X-API-Key: sk_live_XXXXX
```

**Query Parameters:**
- `search` (opcional): Búsqueda por nombre/CIF
- `page` (default: 1)
- `pageSize` (default: 50, max: 500)

**Respuesta:**
```json
{
  "items": [
    {
      "id": "uuid",
      "name": "Empresa SL",
      "taxId": "A12345678",
      "email": "contacto@empresa.com",
      "phone": "+34 600 123 456",
      "address": "Calle Principal, 123",
      "city": "Madrid",
      "zipCode": "28001",
      "country": "ES"
    }
  ],
  "totalCount": 25,
  "pageNumber": 1,
  "pageSize": 50,
  "hasNextPage": false
}
```

### Obtener Cliente por ID

```bash
GET /api/v1/clients/{id}
X-API-Key: sk_live_XXXXX
```

### Crear Cliente

```bash
POST /api/v1/clients
X-API-Key: sk_live_XXXXX
Content-Type: application/json

{
  "name": "Empresa SL",
  "taxId": "A12345678",
  "email": "contacto@empresa.com",
  "phone": "+34 600 123 456",
  "address": "Calle Principal, 123",
  "city": "Madrid",
  "zipCode": "28001",
  "country": "ES"
}
```

Respuesta (201 Created): (mismo formato que GET)

### Actualizar Cliente

```bash
PATCH /api/v1/clients/{id}
X-API-Key: sk_live_XXXXX
Content-Type: application/json

{
  "name": "Empresa SL Actualizada",
  "email": "nuevo@empresa.com",
  ...
}
```

---

## ?? REPORTES

### Libro Diario

```bash
GET /api/v1/reports/diario?fechaInicio=2025-01-01&fechaFin=2025-01-31
X-API-Key: sk_live_XXXXX
```

**Query Parameters:**
- `fechaInicio` (requerido): YYYY-MM-DD
- `fechaFin` (requerido): YYYY-MM-DD

**Respuesta:**
```json
{
  "lineas": [
    {
      "fecha": "2025-01-14",
      "numero": "100/2025",
      "cuenta": "4300",
      "descripcion": "Factura A-2025-001",
      "debe": 1210.00,
      "haber": 0,
      "saldo": 1210.00
    }
  ],
  "totalDebe": 5000.00,
  "totalHaber": 5000.00,
  "totalRegistros": 25
}
```

### Mayor Contable

```bash
GET /api/v1/reports/mayor?fechaInicio=2025-01-01&fechaFin=2025-01-31
X-API-Key: sk_live_XXXXX
```

**Respuesta:**
```json
{
  "cuentas": [
    {
      "codigo": "4300",
      "nombre": "Ventas de servicios",
      "tipo": "Ingresos",
      "saldoInicial": 0,
      "totalDebe": 0,
      "totalHaber": 5000.00,
      "saldoFinal": -5000.00
    }
  ],
  "totalDebe": 5000.00,
  "totalHaber": 5000.00,
  "totalCuentas": 12
}
```

### Balance Sheet

```bash
GET /api/v1/reports/balance?fechaCorte=2025-01-31
X-API-Key: sk_live_XXXXX
```

**Respuesta:**
```json
{
  "activo": {
    "nombre": "Activo",
    "lineas": [
      {
        "codigo": "1200",
        "descripcion": "Clientes",
        "monto": 10000.00
      }
    ],
    "total": 25000.00
  },
  "pasivo": {
    "nombre": "Pasivo",
    "lineas": [...],
    "total": 10000.00
  },
  "patrimonio": {
    "nombre": "Patrimonio",
    "lineas": [...],
    "total": 15000.00
  },
  "totalActivo": 25000.00,
  "totalPasivoPatrimonio": 25000.00,
  "estaBalanceado": true
}
```

### Profit & Loss

```bash
GET /api/v1/reports/pyg?fechaInicio=2025-01-01&fechaFin=2025-01-31
X-API-Key: sk_live_XXXXX
```

**Respuesta:**
```json
{
  "ingresos": {
    "nombre": "Ingresos",
    "lineas": [
      {
        "codigo": "4300",
        "descripcion": "Ventas de servicios",
        "monto": 50000.00
      }
    ],
    "subTotal": 50000.00
  },
  "gastos": {
    "nombre": "Gastos",
    "lineas": [
      {
        "codigo": "6200",
        "descripcion": "Suministros",
        "monto": 5000.00
      }
    ],
    "subTotal": 10000.00
  },
  "totalIngresos": 50000.00,
  "totalGastos": 10000.00,
  "resultadoBruto": 40000.00,
  "resultadoNeto": 40000.00
}
```

---

## ?? Códigos de Error

| Código | Descripción | Ejemplo |
|--------|-------------|---------|
| 200 | OK | Solicitud exitosa |
| 201 | Created | Recurso creado |
| 400 | Bad Request | Datos inválidos |
| 401 | Unauthorized | API Key inválida |
| 404 | Not Found | Recurso no encontrado |
| 429 | Too Many Requests | Rate limit excedido |
| 500 | Server Error | Error del servidor |

**Error Response:**
```json
{
  "error": "Descripción del error",
  "details": "Información adicional (opcional)"
}
```

---

## ? Rate Limiting

Cada API Key tiene un límite configurable de requests por minuto.

**Headers de respuesta:**
```
X-RateLimit-Limit: 1000
X-RateLimit-Remaining: 999
X-RateLimit-Reset: 1705238400
```

Si se excede el límite:
```
HTTP 429 Too Many Requests
Retry-After: 60
```

---

## ?? Ejemplos de Código

### JavaScript/Node.js

```javascript
const API_KEY = 'sk_live_XXXXXXXX';
const BASE_URL = 'https://api.tudominio.com/api/v1';

// Obtener facturas
async function getInvoices() {
  const response = await fetch(`${BASE_URL}/invoices?status=Paid`, {
    headers: {
      'X-API-Key': API_KEY,
      'Content-Type': 'application/json'
    }
  });
  
  const data = await response.json();
  return data;
}

// Crear factura
async function createInvoice(invoice) {
  const response = await fetch(`${BASE_URL}/invoices`, {
    method: 'POST',
    headers: {
      'X-API-Key': API_KEY,
      'Content-Type': 'application/json'
    },
    body: JSON.stringify(invoice)
  });
  
  return response.json();
}
```

### Python

```python
import requests

API_KEY = 'sk_live_XXXXXXXX'
BASE_URL = 'https://api.tudominio.com/api/v1'

headers = {
    'X-API-Key': API_KEY,
    'Content-Type': 'application/json'
}

# Obtener facturas
response = requests.get(f'{BASE_URL}/invoices', headers=headers)
invoices = response.json()

# Crear factura
invoice_data = {
    'clientId': 'uuid',
    'series': 'A',
    'dueDate': '2025-02-14',
    'lines': [...]
}

response = requests.post(f'{BASE_URL}/invoices', json=invoice_data, headers=headers)
new_invoice = response.json()
```

### cURL

```bash
# Obtener facturas
curl -X GET "https://api.tudominio.com/api/v1/invoices?status=Paid" \
  -H "X-API-Key: sk_live_XXXXXXXX"

# Crear factura
curl -X POST "https://api.tudominio.com/api/v1/invoices" \
  -H "X-API-Key: sk_live_XXXXXXXX" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "uuid",
    "series": "A",
    "dueDate": "2025-02-14",
    "lines": [...]
  }'
```

---

## ?? Swagger/OpenAPI

Swagger UI disponible en:

```
https://api.tudominio.com/swagger/index.html
```

JSON Schema en:

```
https://api.tudominio.com/swagger/v1/swagger.json
```

---

## ?? Seguridad

- ? HTTPS obligatorio en producción
- ? API Keys hasheadas en base de datos
- ? Rate limiting por API Key
- ? Validación de IP (opcional)
- ? Logs de todas las requests
- ? Multi-tenant (datos aislados por tenant)

---

## ?? Soporte

Para problemas o preguntas:

- ?? Email: api-support@tudominio.com
- ?? Slack: #api-support
- ?? Issues: github.com/tudominio/erp

---

**Versión:** 1.0  
**Última actualización:** 2025-01-14  
**Status:** Producción ?

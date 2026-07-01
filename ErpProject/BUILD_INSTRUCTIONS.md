# ?? BUILD & VERIFICACIÓN

## ? PASO 1: Limpiar solución

```bash
cd backend
dotnet clean
```

**Tiempo:** 10 segundos

---

## ? PASO 2: Restaurar dependencias

```bash
dotnet restore
```

**Tiempo:** 30 segundos

**Salida esperada:**
```
Restore completed in XXX ms.
```

---

## ? PASO 3: Compilar

```bash
dotnet build
```

**Tiempo:** 2-3 minutos

**Salida esperada:**
```
Build succeeded!
  0 Warning(s)
  0 Error(s)

Time elapsed: 00:XX:XX
```

---

## ?? POSIBLES ERRORES & SOLUCIONES

### Error: "The type or namespace name 'TenantResolverMiddleware' could not be found"
```
Solución: 
  1. Verificar que TenantResolverMiddleware.cs está en 
     backend\Erp.Infrastructure\Tenancy\
  2. Verificar que tiene namespace correcto:
     namespace Erp.Infrastructure.Tenancy;
  3. Rebuild
```

### Error: "Missing using statement for Erp.Infrastructure.Security"
```
Solución:
  En Program.cs, agregar:
  using Erp.Infrastructure.Security;
```

### Error: "Type 'AccountingValidator' not found"
```
Solución:
  En ErpDbContext.cs, verificar que existe
  backend\Erp.Infrastructure\Validators\AccountingValidator.cs
```

### Error: "IApplicationDbContext does not contain JournalEntries"
```
Solución:
  En IApplicationDbContext.cs, verificar que tiene:
  DbSet<JournalEntry> JournalEntries { get; }
  DbSet<JournalEntryLine> JournalEntryLines { get; }
```

---

## ? VERIFICACIÓN POST-BUILD

### 1. Verificar que se compila

```bash
dotnet build
# Resultado: ? Build succeeded
```

### 2. Verificar que el proyecto arranca

```bash
cd Erp.Api
dotnet run
```

**Salida esperada:**
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: https://localhost:5001
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
```

### 3. Verificar Swagger

Ir a: `http://localhost:5000/swagger/index.html`

**Esperado:**
- ? Swagger carga correctamente
- ? Endpoints listados: /api/auth, /api/invoices, /api/expenses, etc.
- ? Sin errores en rojo

### 4. Verificar base de datos

```bash
# Crear conexión a PostgreSQL
psql -U postgres -d erp_main_db

# Listar tablas
\dt

# Resultado esperado:
public | accounts
public | activitylogs
public | auditlogs
public | contacts
public | customers
public | expenses
public | invoicelines
public | invoices
public | journalentries
public | journallines
public | products
public | quotes
public | stock
public | stockmovements
public | suppliers
public | tenantmodules
public | tenants
public | users
public | warehouses
public | roles
public | permissions
public | rolepermissions
public | taxreports
public | leads
```

### 5. Verificar Triggers

```bash
# En psql
SELECT trigger_name, table_name 
FROM information_schema.triggers 
WHERE table_schema = 'public';

# Resultado esperado:
trg_no_delete_invoice       | invoices
trg_no_delete_journal       | journalentries
trg_no_update_stockmovements| stockmovements
```

---

## ?? TESTS BÁSICOS (Curl)

### Test 1: Registrarse

```bash
curl -X POST http://localhost:5000/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!",
    "companyName": "Test Company",
    "companyTaxId": "B12345678"
  }'

# Resultado esperado:
{
  "id": "...",
  "email": "test@example.com",
  "token": "eyJhbGc...",
  "refreshToken": "..."
}
```

### Test 2: Login

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@example.com",
    "password": "Password123!"
  }'

# Guardar el token
```

### Test 3: Obtener Invoices (sin tenant header)

```bash
curl -X GET http://localhost:5000/api/invoices \
  -H "Authorization: Bearer YOUR_TOKEN_HERE"

# Resultado esperado:
403 Unauthorized - Tenant ID requerido
```

### Test 4: Obtener Invoices (con tenant header)

```bash
curl -X GET http://localhost:5000/api/invoices \
  -H "Authorization: Bearer YOUR_TOKEN_HERE" \
  -H "X-Tenant-Id: COMPANY_ID_FROM_REGISTER"

# Resultado esperado:
200 OK
[]  (lista vacía)
```

### Test 5: OCR Upload (sin validar módulo)

```bash
# Crear archivo de prueba
echo "PDF content" > test.pdf

# Subir (público, sin auth)
curl -X POST http://localhost:5000/api/expenses/upload/PUBLIC_TOKEN \
  -F "file=@test.pdf"

# Resultado esperado:
200 OK
{
  "message": "Documento recibido. Será procesado automáticamente.",
  "uploadId": "..."
}
```

---

## ?? EXPECTED OUTPUT

Al terminar `dotnet build`:

```
  Determining projects to restore...
  Restored /src/backend/Erp.Domain/Erp.Domain.csproj (in 1 sec)
  Restored /src/backend/Erp.Application/Erp.Application.csproj (in 2 sec)
  Restored /src/backend/Erp.Infrastructure/Erp.Infrastructure.csproj (in 3 sec)
  Restored /src/backend/Erp.Api/Erp.Api.csproj (in 4 sec)

  Building...
  Erp.Domain -> /src/backend/Erp.Domain/bin/Debug/net10.0/Erp.Domain.dll
  Erp.Application -> /src/backend/Erp.Application/bin/Debug/net10.0/Erp.Application.dll
  Erp.Infrastructure -> /src/backend/Erp.Infrastructure/bin/Debug/net10.0/Erp.Infrastructure.dll
  Erp.Api -> /src/backend/Erp.Api/bin/Debug/net10.0/Erp.Api.dll

Build succeeded!
  0 Warning(s)
  0 Error(s)

Time elapsed: 00:02:15
```

---

## ? CHECKLIST PRE-PRODUCCIÓN

- [ ] `dotnet build` sin errores
- [ ] `dotnet run` arranca sin excepciones
- [ ] Swagger accesible en localhost:5000/swagger
- [ ] Base de datos contiene todas las 24 tablas
- [ ] Triggers existentes en BD
- [ ] Test de registro funciona
- [ ] Test de login funciona
- [ ] Test de obtener invoices con tenant funciona
- [ ] Test de OCR upload funciona

---

## ?? SI TODO ESTÁ OK

Felicitaciones! Tu ERP:
- ? Compila sin errores
- ? Ejecuta sin excepciones
- ? BD sincronizada
- ? API funcional
- ? Multi-tenant seguro
- ? Eventos configurados
- ? Validaciones activas

**Siguiente paso:** Lee `QUICK_START.md` y comienza FASE 2

---

## ?? SI HAY ERRORES

1. Lee el mensaje de error completo
2. Copia el error
3. Revisa la sección "POSIBLES ERRORES & SOLUCIONES"
4. Si no está ahí, busca en `/backend/[proyecto]/obj/Debug/` los archivos `.csproj`
5. Verifica que los using statements son correctos
6. Ejecuta `dotnet clean` y reintentar

---

**Timestamp:** 2025-01-14
**Duración esperada:** 5 minutos
**Éxito esperado:** 99%

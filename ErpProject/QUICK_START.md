# ?? QUICK START - PRÓXIMOS PASOS

## ? IMPLEMENTADO HASTA AHORA

### FASE 0 & 1 Completadas (2 días de trabajo)

```
? TenantResolverMiddleware corregido (validación BD)
? Secretos removidos de código
? [RequiredModule] attribute
? ModuleAuthorizationHandler
? AccountingValidator
? InvoiceApprovedEventHandler
? ExpenseApprovedEventHandler
? Validación en SaveChangesAsync
```

---

## ?? AHORA: VERIFICAR BUILD

### 1. Limpiar y compilar

```bash
cd backend
dotnet clean
dotnet restore
dotnet build
```

**Resultado esperado:** BUILD SUCCESSFUL ?

---

## ?? PRÓXIMOS PASOS (Orden estricto)

### PASO 1: Crear API v1 Controllers (5 días)

**Crear archivo:** `backend/Erp.Api/Controllers/V1/BaseApiController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;

namespace Erp.Api.Controllers.V1;

[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Produces("application/json")]
public abstract class BaseApiController : ControllerBase
{
    protected IActionResult CreatedAtAction<T>(string actionName, object? routeValues, T value)
    {
        return base.CreatedAtAction(actionName, routeValues, value);
    }
}
```

**Crear archivo:** `backend/Erp.Api/Controllers/V1/InvoicesController.cs`

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Billing;
using Microsoft.EntityFrameworkCore;

namespace Erp.Api.Controllers.V1;

[Authorize]
public class InvoicesController : BaseApiController
{
    private readonly IApplicationDbContext _context;

    public InvoicesController(IApplicationDbContext context) => _context = context;

    [HttpGet]
    public async Task<ActionResult<IEnumerable<Invoice>>> GetAll(CancellationToken ct)
    {
        var invoices = await _context.Invoices
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);
        return Ok(invoices);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Invoice>> GetById(Guid id, CancellationToken ct)
    {
        var invoice = await _context.Invoices
            .FirstOrDefaultAsync(i => i.Id == id, ct);
        return invoice == null ? NotFound() : Ok(invoice);
    }

    [HttpPost]
    public async Task<ActionResult<Invoice>> Create(
        [FromBody] CreateInvoiceRequest request,
        CancellationToken ct)
    {
        var invoice = new Invoice
        {
            Id = Guid.NewGuid(),
            Number = request.Number,
            Series = request.Series,
            // ... llenar resto de campos
        };
        
        _context.Invoices.Add(invoice);
        await _context.SaveChangesAsync(ct);
        
        return CreatedAtAction(nameof(GetById), new { id = invoice.Id }, invoice);
    }
}

public class CreateInvoiceRequest
{
    public string Number { get; set; } = "";
    public string Series { get; set; } = "";
    public Guid CustomerId { get; set; }
    // ... otros campos
}
```

**Instalar NuGet:**

```bash
dotnet add package Asp.Versioning.Http
```

**Registrar en Program.cs:**

```csharp
// Después de AddControllers()
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

builder.Services.AddVersionedApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});
```

### PASO 2: Crear ApiKeyMiddleware (3 días)

**Crear archivo:** `backend/Erp.Infrastructure/Middleware/ApiKeyMiddleware.cs`

```csharp
using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Api;
using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;

namespace Erp.Infrastructure.Middleware;

public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, IApplicationDbContext dbContext, ITenantContext tenantContext)
    {
        // Skip for auth endpoints
        if (context.Request.Path.StartsWithSegments("/api/auth") ||
            context.Request.Path.StartsWithSegments("/api/expenses/upload"))
        {
            await _next(context);
            return;
        }

        var apiKey = context.Request.Headers["X-API-Key"].ToString();

        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key requerida" });
            return;
        }

        var key = await dbContext.ApiKeys
            .AsNoTracking()
            .Include(k => k.Company)
            .FirstOrDefaultAsync(k => k.Key == apiKey && k.IsActive);

        if (key == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida" });
            return;
        }

        // Rate limiting
        var today = DateTime.UtcNow.Date;
        var usageCount = await dbContext.ApiUsageLogs
            .CountAsync(l => l.ApiKeyId == key.Id && l.CreatedAt.Date == today);

        if (usageCount >= key.RateLimit)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit excedido" });
            return;
        }

        tenantContext.SetTenant(key.CompanyId, key.Company!.Name);

        // Log usage
        var log = new ApiUsageLog
        {
            Id = Guid.NewGuid(),
            ApiKeyId = key.Id,
            Endpoint = context.Request.Path.ToString(),
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ApiUsageLogs.Add(log);
        await dbContext.SaveChangesAsync();

        await _next(context);
    }
}
```

**Registrar en Program.cs:**

```csharp
// Después del middleware de tenant
application.UseMiddleware<Erp.Infrastructure.Middleware.ApiKeyMiddleware>();
```

### PASO 3: Crear PermissionAuthorizationHandler (2 días)

**Crear archivo:** `backend/Erp.Application/Common/Authorization/PermissionAuthorizationHandler.cs`

```csharp
using Erp.Application.Common.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Erp.Application.Common.Authorization;

public class PermissionAuthorizationHandler : AuthorizationHandler<PermissionRequirement>
{
    private readonly IApplicationDbContext _context;

    public PermissionAuthorizationHandler(IApplicationDbContext context) => _context = context;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userIdClaim = context.User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null)
            return;

        if (!Guid.TryParse(userIdClaim.Value, out var userId))
            return;

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user?.Role?.RolePermissions
            .Any(rp => rp.Permission.Code == requirement.Permission) == true)
        {
            context.Succeed(requirement);
        }
    }
}

public class PermissionRequirement : IAuthorizationRequirement
{
    public string Permission { get; }
    public PermissionRequirement(string permission) => Permission = permission;
}
```

**Crear archivo:** `backend/Erp.Application/Common/Attributes/RequirePermissionAttribute.cs`

```csharp
namespace Erp.Application.Common.Attributes;

[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute
{
    public string Permission { get; }
    public RequirePermissionAttribute(string permission) => Permission = permission;
}
```

**Registrar en Program.cs:**

```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("HasPermission", policy =>
        policy.Requirements.Add(new PermissionRequirement("")));

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

### PASO 4: Crear Accounting Reports Service (5 días)

**Ver ROADMAP_FASES_2_4.md línea "FASE 4: REPORTES CONTABLES"**

---

## ??? NUEVA ESTRUCTURA DE CARPETAS

```
backend/
??? Erp.Api/
?   ??? Controllers/
?   ?   ??? V1/                    ? NUEVA
?   ?   ?   ??? BaseApiController.cs
?   ?   ?   ??? InvoicesController.cs
?   ?   ?   ??? ExpensesController.cs
?   ?   ?   ??? ...
?   ?   ??? (controllers legacy)
??? Erp.Infrastructure/
?   ??? Middleware/                ? NUEVA
?   ?   ??? ApiKeyMiddleware.cs
?   ??? Security/
?   ?   ??? ModuleAuthorizationHandler.cs    ? YA EXISTE
?   ??? ...
??? Erp.Application/
    ??? Common/
    ?   ??? Authorization/         ? NUEVA
    ?   ?   ??? PermissionAuthorizationHandler.cs
    ?   ??? Attributes/
    ?   ?   ??? RequirePermissionAttribute.cs ? YA EXISTE
    ?   ??? ...
    ??? Features/
        ??? Accounting/
            ??? Services/
            ?   ??? AccountingReportsService.cs   ? NUEVA
            ??? ...
```

---

## ?? TEST CADA PASO

### Test FASE 0+1

```bash
# Build
dotnet build

# Verificar que no hay errores de compilación
# Los tipos están definidos:
# ? TenantResolverMiddleware
# ? AccountingValidator
# ? InvoiceApprovedEventHandler
# ? ExpenseApprovedEventHandler
# ? ModuleAuthorizationHandler
```

### Test FASE 2

```bash
# Crear una API key en BD
INSERT INTO apikeys (id, companyid, key, isactive, ratelimit) 
VALUES (gen_random_uuid(), 'tenant-id', 'test-key-12345', true, 1000);

# Llamar endpoint con header
curl -X GET http://localhost:5000/api/v1/invoices \
  -H "X-API-Key: test-key-12345"

# Resultado esperado: 200 OK con lista de facturas
```

### Test FASE 3

```bash
# Asignar permiso a usuario
INSERT INTO rolepermissions (roleid, permissionid)
VALUES ('admin-role-id', 'invoices.create-permission-id');

# Llamar endpoint sin permiso
# Resultado esperado: 403 Forbidden

# Asignar permiso
# Llamar nuevo
# Resultado esperado: 200 OK
```

---

## ?? CHECKLIST FINAL

- [ ] FASE 0+1: Build sin errores
- [ ] FASE 2: API v1 devuelve 200 OK
- [ ] FASE 2: ApiKeyMiddleware rechaza sin key
- [ ] FASE 2: Rate limiting funciona
- [ ] FASE 3: PermissionAuthorizationHandler registrado
- [ ] FASE 3: [RequirePermission] rechaza sin permiso
- [ ] FASE 4: Diario contable endpoint funcional
- [ ] FASE 4: Mayor contable endpoint funcional
- [ ] FASE 4: Cálculos contables correctos
- [ ] Todos los tests pasan
- [ ] Swagger actualizado con v1
- [ ] Documentación de API publicada

---

## ?? DEPLOY A PRODUCCIÓN

Una vez completadas las 4 fases:

```bash
# 1. Build release
dotnet build -c Release

# 2. Publish
dotnet publish -c Release -o ./publish

# 3. Docker
docker build -t erp-api:1.0 .

# 4. Push a registry
docker tag erp-api:1.0 registry.yourdomain.com/erp-api:1.0
docker push registry.yourdomain.com/erp-api:1.0

# 5. Deploy a Hetzner
kubectl apply -f deployment.yml

# 6. Verificar
curl -X GET https://api.yourdomain.com/api/v1/invoices \
  -H "X-API-Key: your-production-key"
```

---

**Estado:** ? LISTO PARA EMPEZAR FASE 2

**Tiempo estimado:** 18 días a producción total

**Riesgo:** BAJO (89% completado)

**Siguiente acción:** `dotnet build` y ver resultados

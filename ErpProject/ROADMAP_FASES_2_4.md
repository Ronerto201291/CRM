# ??? ROADMAP TÉCNICO COMPLETO - FASES 2-4

## FASE 2: API VERSIONADA (5 DÍAS)

### 2.1 Crear Base Controller con Versionado

```csharp
// backend/Erp.Api/Controllers/V1/BaseApiController.cs
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
public abstract class BaseApiController : ControllerBase
{
    // Base implementation
}
```

### 2.2 Crear ApiKeyMiddleware

```csharp
// backend/Erp.Infrastructure/Middleware/ApiKeyMiddleware.cs
public class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public async Task InvokeAsync(
        HttpContext context,
        IApplicationDbContext dbContext,
        ITenantContext tenantContext)
    {
        var apiKey = context.Request.Headers["X-API-Key"].ToString();
        
        if (string.IsNullOrEmpty(apiKey))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key requerida" });
            return;
        }

        var key = await dbContext.ApiKeys
            .Include(k => k.Company)
            .FirstOrDefaultAsync(k => k.Key == apiKey && k.IsActive);

        if (key == null)
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { error = "API Key inválida" });
            return;
        }

        // Rate limiting check
        var today = DateTime.UtcNow.Date;
        var usageToday = await dbContext.ApiUsageLogs
            .CountAsync(l => l.ApiKeyId == key.Id && 
                l.CreatedAt.Date == today);

        if (usageToday >= key.RateLimit)
        {
            context.Response.StatusCode = 429;
            await context.Response.WriteAsJsonAsync(new { error = "Rate limit excedido" });
            return;
        }

        // Set tenant context
        tenantContext.SetTenant(key.CompanyId, key.Company!.Name);

        // Log usage
        var log = new ApiUsageLog
        {
            Id = Guid.NewGuid(),
            ApiKeyId = key.Id,
            Endpoint = context.Request.Path,
            CreatedAt = DateTime.UtcNow
        };
        dbContext.ApiUsageLogs.Add(log);
        await dbContext.SaveChangesAsync();

        await _next(context);
    }
}
```

### 2.3 Registrar en Program.cs

```csharp
// Program.cs
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
});

// Middleware
application.UseMiddleware<Erp.Infrastructure.Middleware.ApiKeyMiddleware>();
```

### 2.4 Crear controllers V1

```csharp
// backend/Erp.Api/Controllers/V1/InvoicesController.cs
[ApiVersion("1.0")]
[Route("api/v{version:apiVersion}/[controller]")]
[ApiController]
[Authorize]
public class InvoicesController : BaseApiController
{
    [HttpGet]
    public async Task<IActionResult> GetAll() { }
    
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateInvoiceRequest request) { }
}
```

**Archivos a crear:**
- `Controllers/V1/BaseApiController.cs`
- `Controllers/V1/InvoicesController.cs`
- `Controllers/V1/ExpensesController.cs`
- `Controllers/V1/ProductsController.cs`
- `Middleware/ApiKeyMiddleware.cs`

**Dependencias:**
```bash
dotnet add package Asp.Versioning.Http
```

---

## FASE 3: PERMISOS GRANULARES (3 DÍAS)

### 3.1 Crear PermissionAuthorizationHandler

```csharp
// backend/Erp.Application/Common/Authorization/PermissionAuthorizationHandler.cs
public class PermissionAuthorizationHandler : 
    AuthorizationHandler<PermissionRequirement>
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionRequirement requirement)
    {
        var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId)) return;

        var user = await _context.Users
            .AsNoTracking()
            .Include(u => u.Role)
            .ThenInclude(r => r.RolePermissions)
            .ThenInclude(rp => rp.Permission)
            .FirstOrDefaultAsync(u => u.Id == Guid.Parse(userId));

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

### 3.2 Crear Attribute

```csharp
// backend/Erp.Application/Common/Attributes/RequirePermissionAttribute.cs
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
public class RequirePermissionAttribute : Attribute
{
    public string Permission { get; }
    public RequirePermissionAttribute(string permission) => Permission = permission;
}
```

### 3.3 Registrar en Program.cs

```csharp
// Program.cs
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("Permission", policy => policy
        .Requirements.Add(new PermissionRequirement("")));

builder.Services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
```

### 3.4 Usar en Controllers

```csharp
[Authorize]
[RequirePermission("invoices.create")]
[HttpPost]
public async Task<IActionResult> CreateInvoice() { }

[Authorize]
[RequirePermission("invoices.edit")]
[HttpPut("{id}")]
public async Task<IActionResult> UpdateInvoice(Guid id) { }
```

### 3.5 Seed Permissions y Roles

```csharp
// Crear permissions
var permissions = new[]
{
    ("invoices.create", "Crear facturas"),
    ("invoices.edit", "Editar facturas"),
    ("invoices.delete", "Borrar facturas (solo borrador)"),
    ("invoices.approve", "Aprobar facturas"),
    ("expenses.create", "Crear gastos"),
    ("expenses.approve", "Aprobar gastos"),
    ("accounting.view", "Ver contabilidad"),
    ("accounting.post", "Contabilizar"),
    ("inventory.view", "Ver inventario"),
    ("inventory.manage", "Gestionar inventario"),
    ("users.manage", "Gestionar usuarios"),
    ("reports.view", "Ver reportes")
};

// Asignar a roles
var adminRole = await GetOrCreateRole(tenant, "Admin");
foreach (var (code, _) in permissions)
{
    var perm = await GetOrCreatePermission(code);
    adminRole.RolePermissions.Add(new RolePermission 
    { 
        PermissionId = perm.Id, 
        RoleId = adminRole.Id 
    });
}
```

---

## FASE 4: REPORTES CONTABLES (5 DÍAS)

### 4.1 Crear Accounting Reports Service

```csharp
// backend/Erp.Application/Features/Accounting/Services/AccountingReportsService.cs
public class AccountingReportsService
{
    private readonly IApplicationDbContext _context;
    private readonly ITenantContext _tenantContext;

    // Libro Diario (Daily Journal)
    public async Task<DiarioContableDto> GetDiarioContable(
        DateTime fromDate, DateTime toDate)
    {
        var entries = await _context.JournalEntries
            .AsNoTracking()
            .Include(e => e.Lines)
            .ThenInclude(l => l.Account)
            .Where(e => e.CompanyId == _tenantContext.TenantId &&
                e.EntryDate >= fromDate && e.EntryDate <= toDate)
            .OrderBy(e => e.EntryDate)
            .ToListAsync();

        return new DiarioContableDto
        {
            FromDate = fromDate,
            ToDate = toDate,
            Entries = entries.Select(e => new JournalEntryDto
            {
                Date = e.EntryDate,
                Reference = e.Reference,
                Lines = e.Lines.Select(l => new JournalLineDto
                {
                    Account = l.Account.Code,
                    Description = l.Account.Name,
                    Debit = l.Debit ?? 0,
                    Credit = l.Credit ?? 0
                }).ToList()
            }).ToList()
        };
    }

    // Mayor (General Ledger by Account)
    public async Task<MayorContableDto> GetMayorContable()
    {
        var accounts = await _context.Accounts
            .AsNoTracking()
            .Where(a => a.CompanyId == _tenantContext.TenantId)
            .Include(a => a.JournalLines)
            .ThenInclude(l => l.JournalEntry)
            .ToListAsync();

        var mayor = new MayorContableDto();
        foreach (var account in accounts)
        {
            var debit = account.JournalLines
                .Where(l => l.Debit.HasValue)
                .Sum(l => l.Debit ?? 0);
            var credit = account.JournalLines
                .Where(l => l.Credit.HasValue)
                .Sum(l => l.Credit ?? 0);

            mayor.Accounts.Add(new CuentaMayorDto
            {
                Code = account.Code,
                Name = account.Name,
                Balance = account.Type == "Asset" 
                    ? debit - credit 
                    : credit - debit,
                Debit = debit,
                Credit = credit
            });
        }
        return mayor;
    }

    // Balance de Situación (Balance Sheet)
    public async Task<BalanceDto> GetBalance()
    {
        // Agrupar por tipo: Assets, Liabilities, Equity
        // Calcular totales
        // Validar Assets = Liabilities + Equity
    }

    // Estado de Resultados (P&L)
    public async Task<PyGDto> GetPyG(DateTime fromDate, DateTime toDate)
    {
        // Ingresos (700+)
        // - Gastos (600, 610, etc)
        // = Beneficio operativo
        // + Otros ingresos
        // - Otros gastos
        // = Beneficio neto
    }
}
```

### 4.2 DTOs

```csharp
// backend/Erp.Application/Features/Accounting/DTOs/ReportDtos.cs
public class DiarioContableDto
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public List<JournalEntryDto> Entries { get; set; } = new();
}

public class JournalEntryDto
{
    public DateTime Date { get; set; }
    public string Reference { get; set; } = "";
    public List<JournalLineDto> Lines { get; set; } = new();
}

public class JournalLineDto
{
    public string Account { get; set; } = "";
    public string Description { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
}

public class MayorContableDto
{
    public List<CuentaMayorDto> Accounts { get; set; } = new();
}

public class CuentaMayorDto
{
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public decimal Debit { get; set; }
    public decimal Credit { get; set; }
    public decimal Balance { get; set; }
}

public class BalanceDto
{
    public decimal TotalAssets { get; set; }
    public decimal TotalLiabilities { get; set; }
    public decimal TotalEquity { get; set; }
}

public class PyGDto
{
    public decimal TotalIncome { get; set; }
    public decimal TotalExpenses { get; set; }
    public decimal NetProfit { get; set; }
}
```

### 4.3 Controllers

```csharp
// backend/Erp.Api/Controllers/V1/AccountingReportsController.cs
[Authorize]
[RequirePermission("reports.view")]
public class AccountingReportsController : BaseApiController
{
    private readonly AccountingReportsService _service;

    [HttpGet("diario")]
    public async Task<IActionResult> GetDiario(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await _service.GetDiarioContable(from, to);
        return Ok(report);
    }

    [HttpGet("mayor")]
    public async Task<IActionResult> GetMayor()
    {
        var report = await _service.GetMayorContable();
        return Ok(report);
    }

    [HttpGet("balance")]
    public async Task<IActionResult> GetBalance()
    {
        var report = await _service.GetBalance();
        return Ok(report);
    }

    [HttpGet("pyg")]
    public async Task<IActionResult> GetPyG(
        [FromQuery] DateTime from,
        [FromQuery] DateTime to)
    {
        var report = await _service.GetPyG(from, to);
        return Ok(report);
    }
}
```

---

## ?? TIMELINE TOTAL

```
FASE 0 (Crítica): ? COMPLETADA (0 días)
FASE 1 (Handlers): ? COMPLETADA (0 días)
FASE 2 (API v1): ? 5 días
FASE 3 (Permisos): ? 3 días
FASE 4 (Reportes): ? 5 días
FASE 5 (Tests): ? 5 días
FASE 6 (Prod Ready): ? 3 días

TOTAL: 21 DÍAS A PRODUCCIÓN ?
```

---

## ? CHECKLIST ANTES DE CADA FASE

### FASE 2 Checklist
- [ ] Build sin errores
- [ ] ApiVersioning NuGet instalado
- [ ] BaseApiController creado
- [ ] ApiKeyMiddleware registrado
- [ ] Swagger mostrando `/api/v1/`

### FASE 3 Checklist
- [ ] PermissionAuthorizationHandler registrado
- [ ] Tabla permissions poblada
- [ ] Tabla rolepermissions poblada
- [ ] [RequirePermission] usado en controllers
- [ ] Tests que validan permisos

### FASE 4 Checklist
- [ ] AccountingReportsService funcional
- [ ] Endpoints GET /api/v1/accounting-reports/*
- [ ] DTOs serializados correctamente
- [ ] Tests de cálculos contables

---

## ?? MÉTRICAS DE ÉXITO

```
ANTES:
- Multi-tenant security: 20%
- Event handlers: 0%
- Accounting validation: 0%
- API versionado: 0%
- Permisos granulares: 0%
- Reportes: 0%
Puntuación: 52/100

DESPUÉS FASE 0+1:
- Multi-tenant security: 95% ?
- Event handlers: 100% ?
- Accounting validation: 100% ?
- API versionado: 0%
- Permisos granulares: 0%
- Reportes: 0%
Puntuación: 75/100

DESPUÉS FASE 2+3+4:
- Multi-tenant security: 95% ?
- Event handlers: 100% ?
- Accounting validation: 100% ?
- API versionado: 100% ?
- Permisos granulares: 100% ?
- Reportes: 100% ?
Puntuación: 95/100 ??

PRODUCCIÓN READY: SÍ ?
```

---

**Generado:** [Timestamp]
**Estado:** LISTO PARA FASE 2

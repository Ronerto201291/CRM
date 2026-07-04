using Asp.Versioning;
using Erp.Api.HealthChecks;
using Erp.Infrastructure.Data;
using Erp.Infrastructure.Services;
using Erp.Infrastructure.Tenancy;
using Hangfire;
using Hangfire.PostgreSql;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Text;
using System.Text.Json;
using Erp.Application;
using Erp.Application.Modularity;
using Erp.Infrastructure;
using Erp.Modules.Billing.Infrastructure;
using Erp.Modules.Crm.Infrastructure;
using Erp.Modules.Inventory.Infrastructure;
using Erp.Modules.Accounting.Infrastructure;
using Erp.Modules.Expenses.Infrastructure;
using Erp.Modules.Treasury.Infrastructure;
using Erp.Modules.Payroll.Infrastructure;
using Erp.Modules.Purchasing.Infrastructure;
using Erp.Modules.Sales.Infrastructure;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

// 1. Configure Serilog
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

builder.Host.UseSerilog((context, services, configuration) => configuration
    .ReadFrom.Configuration(context.Configuration)
    .ReadFrom.Services(services)
    .Enrich.FromLogContext()
    .WriteTo.Console());

var env = builder.Environment;
var isIntegrationTest = env.IsEnvironment("IntegrationTests");

// 2. Add services to the container.
// AbacAuthorizationFilter is added to MVC filters so it runs on every controller action.
// It is a no-op on endpoints without [RequirePermission].
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllers(options =>
{
    // ABAC filter runs AFTER ModuleAuthorizationFilter (module-level RBAC).
    // Provides action-level granularity within a module.
    options.Filters.AddService<Erp.Infrastructure.Security.AbacAuthorizationFilter>();
})
    .AddErpModuleControllers(Erp.Modules.Accounting.Api.AccountingErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Billing.Api.BillingErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Crm.Api.CrmErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Inventory.Api.InventoryErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Expenses.Api.ExpensesErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Treasury.Api.TreasuryErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Payroll.Api.PayrollErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Purchasing.Api.PurchasingErpModule.Instance)
    .AddErpModuleControllers(Erp.Modules.Sales.Api.SalesErpModule.Instance);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// OpenTelemetry métricas + tracing OTLP opcional (ADR-0018 #36)
var otlpEndpoint = builder.Configuration["OpenTelemetry:OtlpEndpoint"];
builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("Erp.Api"))
    .WithMetrics(metrics => metrics
        .AddAspNetCoreInstrumentation()
        .AddRuntimeInstrumentation()
        .AddPrometheusExporter())
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();
        if (!string.IsNullOrWhiteSpace(otlpEndpoint))
        {
            tracing.AddOtlpExporter(options => options.Endpoint = new Uri(otlpEndpoint));
        }
    });

// JWT Auth Setup
var jwtSecret = builder.Configuration["Jwt:Secret"];
var jwtIssuer = builder.Configuration["Jwt:Issuer"];
var jwtAudience = builder.Configuration["Jwt:Audience"];

if (string.IsNullOrEmpty(jwtSecret) || string.IsNullOrEmpty(jwtIssuer) || string.IsNullOrEmpty(jwtAudience))
{
    throw new InvalidOperationException(
        "JWT configuration missing. Set Jwt:Secret, Jwt:Issuer, and Jwt:Audience in appsettings or environment variables.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

// ? CORS para Frontend
builder.Services.AddCors(options =>
{
    var allowedOrigins = new[]
    {
        "http://localhost:3000",        // Dev local
        "http://localhost:3001",
        "http://127.0.0.1:3000",
        "http://frontend",              // Docker network
        "http://localhost",             // Docker fallback
        "https://erp.tudominio.com",    // Producci�n
        "https://www.erp.tudominio.com",
    };

    options.AddDefaultPolicy(policy =>
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE", "OPTIONS")
            .AllowCredentials()
            .WithExposedHeaders(
                "X-Total-Count",
                "Authorization",
                Erp.Application.Common.Fiscal.FiscalExportHeaders.ExportDisclaimerHeader,
                Erp.Application.Common.Fiscal.FiscalExportHeaders.OfficialFormatHeader)
    );
});

builder.Services.AddAuthorization();

// 3. Configure Database connection
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrEmpty(connectionString))
{
    throw new InvalidOperationException(
        "Database connection string missing. Set ConnectionStrings__DefaultConnection in environment or appsettings.");
}

builder.Services.AddScoped<Erp.Infrastructure.Tenancy.PostgresTenantSessionInterceptor>();
builder.Services.AddDbContext<ErpDbContext>((sp, options) =>
    options.UseNpgsql(connectionString)
           .AddInterceptors(
               sp.GetRequiredService<Erp.Infrastructure.Interceptors.AuditSaveChangesInterceptor>(),
               sp.GetRequiredService<Erp.Infrastructure.Tenancy.PostgresTenantSessionInterceptor>())
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// 4. Configure Multi-tenancy and Infrastructure
builder.Services.AddScoped<Erp.Application.Common.Interfaces.ITenantContext, TenantContext>();

// Authorization with module enforcement
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("ModuleRequired", policy => policy.RequireAuthenticatedUser());

builder.Services.AddScoped<Microsoft.AspNetCore.Authorization.IAuthorizationHandler, 
    Erp.Infrastructure.Security.ModuleAuthorizationHandler>();

builder.Services.AddScoped<Microsoft.AspNetCore.Mvc.Filters.IAsyncAuthorizationFilter,
    Erp.Infrastructure.Security.ModuleAuthorizationFilter>();

builder.Services.AddInfrastructureServices();

if (isIntegrationTest)
{
    builder.Services.RemoveAll<Erp.Application.Common.Interfaces.IEmailService>();
    builder.Services.AddScoped<Erp.Application.Common.Interfaces.IEmailService, NoOpEmailService>();
}
builder.Services.AddApplicationServices();

// Per-module DbContexts (todos vía IErpModule — ADR-0018 #19e)
builder.Services.AddErpModule(Erp.Modules.Accounting.Api.AccountingErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Billing.Api.BillingErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Crm.Api.CrmErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Inventory.Api.InventoryErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Expenses.Api.ExpensesErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Treasury.Api.TreasuryErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Payroll.Api.PayrollErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Purchasing.Api.PurchasingErpModule.Instance, builder.Configuration);
builder.Services.AddErpModule(Erp.Modules.Sales.Api.SalesErpModule.Instance, builder.Configuration);

if (isIntegrationTest)
{
    builder.Services.RemoveAll<Erp.Modules.Billing.Application.Interfaces.IVerifactuSubmissionGateway>();
    builder.Services.AddScoped<Erp.Modules.Billing.Application.Interfaces.IVerifactuSubmissionGateway,
        Erp.Modules.Billing.Infrastructure.Services.NoOpVerifactuSubmissionGateway>();
}

// RabbitMQ is optional — if disabled, Hangfire OutboxProcessorJob is the fallback transport.
// Set RabbitMQ:Enabled = false in appsettings to run without a RabbitMQ instance.
var rabbitEnabled = builder.Configuration.GetValue<bool>("RabbitMQ:Enabled", false);
if (rabbitEnabled)
{
    builder.Services.AddRabbitMqMessaging();
}

// Todos los módulos de negocio registran MediatR vía IErpModule (#19e)

// Infrastructure automation handlers (realtime rule evaluation)
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(
        typeof(Erp.Infrastructure.Automation.LeadStatusChangedRuleHandler).Assembly));

// 5. Configure Redis (or in-memory cache for integration tests)
if (isIntegrationTest)
{
    builder.Services.AddDistributedMemoryCache();
}
else
{
    var redisString = builder.Configuration.GetConnectionString("Redis");
    if (string.IsNullOrEmpty(redisString))
        throw new InvalidOperationException("Redis connection string missing. Set ConnectionStrings__Redis in environment.");
    builder.Services.AddStackExchangeRedisCache(options =>
    {
        options.Configuration = redisString;
    });
}

// 6. Configure Hangfire (skipped in integration tests — no DB required at host startup)
if (!isIntegrationTest)
{
    builder.Services.AddHangfire(config => config
        .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
        .UseSimpleAssemblyNameTypeSerializer()
        .UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(c => c.UseNpgsqlConnection(connectionString)));
    
    builder.Services.AddHangfireServer();
}

// 7. Health Checks (DB + Redis + optional RabbitMQ)
var healthChecks = builder.Services.AddHealthChecks();
if (isIntegrationTest)
{
    healthChecks
        .AddCheck("postgres", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("stub"), tags: new[] { "db", "ready" })
        .AddCheck("redis", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy("stub"), tags: new[] { "cache", "ready" });
}
else
{
    healthChecks
        .AddCheck<PostgresHealthCheck>("postgres", tags: new[] { "db", "ready" })
        .AddCheck<RedisHealthCheck>("redis",    tags: new[] { "cache", "ready" });
}

// 8. OCR Background Service
// 9. API versioning support (route-based v1)
builder.Services.AddApiVersioning(options =>
{
    options.DefaultApiVersion = new Asp.Versioning.ApiVersion(1, 0);
    options.AssumeDefaultVersionWhenUnspecified = true;
    options.ReportApiVersions = true;
}).AddApiExplorer(options =>
{
    options.GroupNameFormat = "'v'VVV";
    options.SubstituteApiVersionInUrl = true;
});

var application = builder.Build();
var appStartedUtc = DateTime.UtcNow;

// Configure the HTTP request pipeline.
if (application.Environment.IsDevelopment())
{
    application.UseSwagger();
    application.UseSwaggerUI();
}

application.UseSerilogRequestLogging();
application.UseMiddleware<Erp.Infrastructure.Middleware.ExceptionHandlingMiddleware>();
application.UseCors();  // ? AGREGAR CORS MIDDLEWARE
application.UseStaticFiles();
application.UseHttpsRedirection();

// API pública v1: validación de X-Api-Key + rate limit (antes de tenant JWT)
application.UseMiddleware<Erp.Infrastructure.Security.ApiKeyRateLimitMiddleware>();

// Multi-tenant Middleware
application.UseMiddleware<TenantResolverMiddleware>();

// Login brute-force protection (max 10 attempts / IP / minute via Redis)
application.UseMiddleware<Erp.Infrastructure.Security.LoginRateLimitMiddleware>();

application.UseAuthentication();
application.UseMiddleware<Erp.Infrastructure.Security.TenantMembershipMiddleware>();
application.UseAuthorization();
application.MapControllers();

// Métricas scrape Prometheus (sin auth — uso interno /monitoring)
application.MapPrometheusScrapingEndpoint("/metrics");

// Health Check endpoints (no auth required — used by load balancers / container orchestrators)
application.MapGet("/health/info", (IWebHostEnvironment env) => Results.Json(new
{
    service = "Erp.Api",
    environment = env.EnvironmentName,
    uptimeSeconds = (int)(DateTime.UtcNow - appStartedUtc).TotalSeconds,
    version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "0.0.0",
}, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));

application.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        var result = new
        {
            status  = report.Status.ToString(),
            checks  = report.Entries.Select(e => new
            {
                name        = e.Key,
                status      = e.Value.Status.ToString(),
                description = e.Value.Description,
                duration    = e.Value.Duration.TotalMilliseconds
            }),
            totalDuration = report.TotalDuration.TotalMilliseconds
        };
        await ctx.Response.WriteAsJsonAsync(result,
            new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
});

// Liveness probe — just checks that the process is alive (no dependency checks)
application.MapHealthChecks("/health/live", new HealthCheckOptions
{
    Predicate = _ => false  // skip all registered checks
});

// Readiness probe — only checks critical dependencies (db + cache)
application.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

// Hangfire Dashboard — restringido a localhost en producción
if (!isIntegrationTest)
{
    application.UseHangfireDashboard("/hangfire", new DashboardOptions
    {
        Authorization = new[] { new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter() }
    });
}

// Hangfire recurring jobs — retry on transient DNS/socket errors at startup
if (!isIntegrationTest)
{
for (int attempt = 1; attempt <= 10; attempt++)
{
    try
    {
        RecurringJob.AddOrUpdate<Erp.Infrastructure.Services.OutboxProcessorJob>(
            "outbox-processor",
            job => job.ProcessPendingAsync(CancellationToken.None),
            Cron.Minutely());

        RecurringJob.AddOrUpdate<Erp.Modules.Billing.Infrastructure.Services.ExpireQuotesJob>(
            "expire-quotes",
            job => job.ExecuteAsync(),
            "0 1 * * *");

        RecurringJob.AddOrUpdate<Erp.Infrastructure.Services.FiscalReminderJob>(
            "fiscal-reminder",
            job => job.ExecuteAsync(),
            "0 8 * * *"); // Diario a las 8:00

        RecurringJob.AddOrUpdate<Erp.Infrastructure.Automation.RuleEvaluatorJob>(
            "rule-evaluator",
            job => job.EvaluateRulesAsync(CancellationToken.None),
            "0 9 * * *"); // Diario a las 9:00 — facturas vencidas + stock bajo

        Erp.Modules.Accounting.Infrastructure.DependencyInjection.RegisterAccountingRecurringJobs();
        break;
    }
    catch (Exception ex) when (attempt < 10)
    {
        Console.WriteLine($"[Hangfire] Startup attempt {attempt}/10 failed: {ex.Message}. Retrying in 3s...");
        Thread.Sleep(3000);
    }
}
}

// Auto-Migrate and Seed
if (!isIntegrationTest)
{
using (var scope = application.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
    var accountingDb = scope.ServiceProvider.GetRequiredService<Erp.Modules.Accounting.Infrastructure.Data.AccountingDbContext>();

    try
    {
        // Apply pending EF migrations on every startup.
        // Safe: MigrateAsync is idempotent — already-applied migrations are skipped.
        await dbContext.Database.MigrateAsync();

        // Per-module migrations — each module owns its schema and migration history.
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Billing.Infrastructure.Data.BillingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Crm.Infrastructure.Data.CrmDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Inventory.Infrastructure.Data.InventoryDbContext>().Database.MigrateAsync();
        await accountingDb.Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Expenses.Infrastructure.Data.ExpensesDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Treasury.Infrastructure.Data.TreasuryDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Payroll.Infrastructure.Data.PayrollDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Purchasing.Infrastructure.Data.PurchasingDbContext>().Database.MigrateAsync();
        await scope.ServiceProvider.GetRequiredService<Erp.Modules.Sales.Infrastructure.Data.SalesDbContext>().Database.MigrateAsync();
        Log.Information("All module migrations applied (Purchasing, Sales, Inventory + others).");

        await Erp.Infrastructure.Tenancy.PostgresRlsBootstrap.ApplyPilotPoliciesAsync(
            dbContext,
            builder.Configuration,
            scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("PostgresRls"),
            CancellationToken.None);
        
        // Seed on first boot when the database has no users (any environment).
        // IgnoreQueryFilters: at startup there is no tenant context, so filtered Any() is always false.
        // Set Seed__AdminPassword in env / docker-compose to enable bootstrap admin.
        if (!dbContext.Users.IgnoreQueryFilters().Any())
        {
            var seedPassword = builder.Configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(seedPassword))
            {
                Log.Warning(
                    "Database has no users but Seed:AdminPassword is not configured — skipping seed. " +
                    "Register via /signup or set Seed__AdminPassword (e.g. docker-compose.override.yml).");
            }
            else
            {
            var company = dbContext.Companies.IgnoreQueryFilters().FirstOrDefault()
                ?? new Erp.Domain.Entities.Core.Company
                {
                    Id = Guid.NewGuid(),
                    Name = "DevCorp S.A.",
                    TaxId = "B12345678",
                    PublicUploadToken = Guid.NewGuid().ToString("N"),
                    QrUploadEnabled = true
                };
            if (dbContext.Entry(company).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
            {
                dbContext.Companies.Add(company);
                dbContext.SaveChanges();
            }

            // Reuse or seed roles
            var adminRole = dbContext.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.CompanyId == company.Id && r.Name == "Admin")
                ?? new Erp.Domain.Entities.Core.Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Admin" };
            var managerRole = dbContext.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.CompanyId == company.Id && r.Name == "Manager")
                ?? new Erp.Domain.Entities.Core.Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Manager" };
            var contableRole = dbContext.Roles.IgnoreQueryFilters().FirstOrDefault(r => r.CompanyId == company.Id && r.Name == "Contable")
                ?? new Erp.Domain.Entities.Core.Role { Id = Guid.NewGuid(), CompanyId = company.Id, Name = "Contable" };
            if (dbContext.Entry(adminRole).State == Microsoft.EntityFrameworkCore.EntityState.Detached)
                dbContext.Roles.AddRange(adminRole, managerRole, contableRole);
            dbContext.SaveChanges();

            if (!dbContext.Users.IgnoreQueryFilters().Any(u => u.Email == "admin@devcorp.com"))
            {
                var adminUser = new Erp.Domain.Entities.Core.User
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Email = "admin@devcorp.com",
                    FirstName = "Admin",
                    LastName = "System",
                    IsActive = true,
                    RoleId = adminRole.Id,
                    PasswordHash = BCrypt.Net.BCrypt.HashPassword(seedPassword, workFactor: 12)
                };
                dbContext.Users.Add(adminUser);
                dbContext.UserCompanies.Add(new Erp.Domain.Entities.Core.UserCompany
                {
                    Id = Guid.NewGuid(),
                    UserId = adminUser.Id,
                    CompanyId = company.Id,
                    RoleId = adminRole.Id,
                    IsDefault = true
                });
            }

            // Seed Plan General Contable español (cuentas mínimas PGC 2007)
            var pgcAccounts = new[]
            {
                // Grupo 1 – Financiación básica
                ("100", "Capital social", "Patrimonio"),
                ("118", "Aportaciones de socios", "Patrimonio"),
                ("129", "Resultado del ejercicio", "Patrimonio"),
                // Grupo 2 – Activo no corriente
                ("210", "Terrenos y bienes naturales", "Activo"),
                ("211", "Construcciones", "Activo"),
                ("213", "Maquinaria", "Activo"),
                ("216", "Mobiliario", "Activo"),
                ("217", "Equipos para procesos de información", "Activo"),
                ("218", "Elementos de transporte", "Activo"),
                ("280", "Amortización acumulada inmovilizado material", "Activo"),
                // Grupo 3 – Existencias
                ("300", "Mercaderías", "Activo"),
                ("310", "Materias primas", "Activo"),
                ("350", "Productos terminados", "Activo"),
                // Grupo 4 – Acreedores y deudores comerciales
                ("400", "Proveedores", "Pasivo"),
                ("410", "Acreedores por prestaciones de servicios", "Pasivo"),
                ("430", "Clientes", "Activo"),
                ("440", "Deudores", "Activo"),
                ("460", "Anticipos de remuneraciones", "Activo"),
                ("470", "Hacienda Pública deudora por IVA", "Activo"),
                ("4700", "Hacienda Pública deudora por IRPF", "Activo"),
                ("472", "Hacienda Pública, IVA soportado", "Activo"),
                ("473", "Hacienda Pública, retenciones y pagos a cuenta", "Activo"),
                ("475", "Hacienda Pública, acreedora por IVA", "Pasivo"),
                ("4751", "Hacienda Pública acreedora por retenciones practicadas", "Pasivo"),
                ("477", "Hacienda Pública, IVA repercutido", "Pasivo"),
                ("476", "Organismos de la Seguridad Social acreedores", "Pasivo"),
                // Grupo 5 – Cuentas financieras
                ("520", "Deudas a corto plazo con entidades de crédito", "Pasivo"),
                ("570", "Caja, euros", "Activo"),
                ("572", "Bancos e instituciones de crédito c/c vista, euros", "Activo"),
                // Grupo 6 – Compras y gastos
                ("600", "Compras de mercaderías", "Gasto"),
                ("601", "Compras de materias primas", "Gasto"),
                ("621", "Arrendamientos y cánones", "Gasto"),
                ("622", "Reparaciones y conservación", "Gasto"),
                ("623", "Servicios de profesionales independientes", "Gasto"),
                ("624", "Transportes", "Gasto"),
                ("625", "Primas de seguros", "Gasto"),
                ("626", "Servicios bancarios y similares", "Gasto"),
                ("627", "Publicidad, propaganda y relaciones públicas", "Gasto"),
                ("628", "Suministros", "Gasto"),
                ("629", "Otros servicios", "Gasto"),
                ("640", "Sueldos y salarios", "Gasto"),
                ("642", "Seguridad Social a cargo de la empresa", "Gasto"),
                ("681", "Amortización del inmovilizado material", "Gasto"),
                // Grupo 7 – Ventas e ingresos
                ("700", "Ventas de mercaderías", "Ingreso"),
                ("701", "Ventas de productos terminados", "Ingreso"),
                ("705", "Prestaciones de servicios", "Ingreso"),
                ("708", "Devoluciones de ventas y operaciones similares", "Ingreso"),
                ("751", "Subvenciones a la explotación", "Ingreso"),
                ("760", "Ingresos de participaciones en instrumentos de patrimonio", "Ingreso"),
                ("770", "Beneficios procedentes del inmovilizado material", "Ingreso"),
            };
            var existingAccountCodes = accountingDb.Accounts
                .IgnoreQueryFilters()
                .Where(a => a.CompanyId == company.Id)
                .Select(a => a.Code)
                .ToHashSet();
            foreach (var (code, name, type) in pgcAccounts)
            {
                if (existingAccountCodes.Contains(code))
                    continue;
                accountingDb.Accounts.Add(new Erp.Modules.Accounting.Domain.Entities.Account
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    Code = code,
                    Name = name,
                    Type = type
                });
            }
            accountingDb.SaveChanges();

            // Seed TenantModules — todos desactivados por defecto excepto Core
            var moduleNames = new[] { "Inventory", "OCR", "PublicApi", "Expenses", "Accounting", "CRM", "Billing" };
            var existingModules = dbContext.TenantModules
                .IgnoreQueryFilters()
                .Where(tm => tm.CompanyId == company.Id)
                .Select(tm => tm.ModuleName)
                .ToHashSet();
            foreach (var moduleName in moduleNames)
            {
                if (existingModules.Contains(moduleName))
                    continue;
                dbContext.TenantModules.Add(new Erp.Domain.Entities.Core.TenantModule
                {
                    Id = Guid.NewGuid(),
                    CompanyId = company.Id,
                    ModuleName = moduleName,
                    IsEnabled = true // Dev: todos activos
                });
            }

            dbContext.SaveChanges();
            Log.Information("Seed completado: Company, 3 Roles, Admin user, PGC ({Count} cuentas), TenantModules.", pgcAccounts.Length);
            }
        }
    }
    catch (Exception ex)
    {
        Log.Fatal(ex, "Database migration or seed failed — aborting startup.");
        throw;
    }
}
}

application.Run();

/// <summary>Expone el entry point para WebApplicationFactory en tests de integración.</summary>
public partial class Program { }

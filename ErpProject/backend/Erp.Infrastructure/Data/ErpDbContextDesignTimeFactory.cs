using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Erp.Infrastructure.Tenancy;

namespace Erp.Infrastructure.Data;

/// <summary>
/// Design-time factory used exclusively by dotnet-ef CLI tools (migrations script, database update, etc.).
///
/// Why this exists:
///   The migration Designer.cs stubs (added to register migrations with EF Core) intentionally have
///   empty BuildTargetModel() methods. EF Core 10 treats the resulting model mismatch as
///   PendingModelChangesWarning (an error by default). This factory suppresses that warning so
///   `dotnet ef database update` can apply all migrations on a clean database.
///
/// This factory is NOT used at runtime — Program.cs registers ErpDbContext via AddDbContext().
/// </summary>
public sealed class ErpDbContextDesignTimeFactory : IDesignTimeDbContextFactory<ErpDbContext>
{
    public ErpDbContext CreateDbContext(string[] args)
    {
        // Load config from Erp.Api project (startup project)
        var basePath = Path.Combine(Directory.GetCurrentDirectory(), "..", "Erp.Api");
        if (!Directory.Exists(basePath))
            basePath = Directory.GetCurrentDirectory(); // fallback when running from Infrastructure dir

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Database=erp_main_db;Username=postgres;Password=postgres";

        var optionsBuilder = new DbContextOptionsBuilder<ErpDbContext>();
        optionsBuilder
            .UseNpgsql(connectionString)
            // Suppress PendingModelChangesWarning — stubs have empty BuildTargetModel by design.
            // Once all migrations are stable, generate proper Designer.cs files via `dotnet ef migrations add`.
            .ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

        return new ErpDbContext(optionsBuilder.Options, new TenantContext());
    }
}

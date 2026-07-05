using System.Reflection;
using Erp.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Xunit;

namespace Erp.ArchitectureTests;

/// <summary>
/// Migraciones SQL manuales (sin .Designer.cs) deben declarar [DbContext] en la clase principal
/// para que EF las descubra en la cadena de migraciones.
/// </summary>
public class ManualMigrationArchitectureTests
{
    [Fact]
    public void ManualSqlMigrations_HaveDbContextAttribute()
    {
        var infra = typeof(Erp.Infrastructure.DependencyInjection).Assembly;
        var violations = infra.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false }
                        && t.Namespace == "Erp.Infrastructure.Migrations"
                        && typeof(Migration).IsAssignableFrom(t)
                        && t.GetCustomAttribute<MigrationAttribute>() != null)
            .Where(t => t.GetCustomAttribute<DbContextAttribute>()?.ContextType != typeof(ErpDbContext))
            .Select(t => t.FullName!)
            .ToList();

        Assert.True(violations.Count == 0,
            "Migraciones SQL manuales deben tener [DbContext(typeof(ErpDbContext))]:\n"
            + string.Join("\n", violations));
    }
}

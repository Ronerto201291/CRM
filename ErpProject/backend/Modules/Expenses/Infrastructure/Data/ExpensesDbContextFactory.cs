using Erp.Infrastructure.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Erp.Modules.Expenses.Infrastructure.Data;

public class ExpensesDbContextFactory : IDesignTimeDbContextFactory<ExpensesDbContext>
{
    public ExpensesDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection not found for design-time factory.");

        var optionsBuilder = new DbContextOptionsBuilder<ExpensesDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ExpensesDbContext(optionsBuilder.Options, new TenantContext());
    }
}

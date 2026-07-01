using Erp.Modules.Expenses.Application.Interfaces;
using Erp.Modules.Expenses.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Expenses.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddExpensesInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<ExpensesDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IExpensesDbContext>(p => p.GetRequiredService<ExpensesDbContext>());

        return services;
    }
}

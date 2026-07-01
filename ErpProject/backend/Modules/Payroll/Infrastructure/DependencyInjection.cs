using Erp.Modules.Payroll.Application.Interfaces;
using Erp.Modules.Payroll.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Payroll.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPayrollInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<PayrollDbContext>(options =>
            options.UseNpgsql(connectionString)
                .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IPayrollDbContext>(p => p.GetRequiredService<PayrollDbContext>());
        return services;
    }
}

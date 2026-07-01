using Erp.Modules.Purchasing.Application.Interfaces;
using Erp.Modules.Purchasing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Purchasing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddPurchasingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<PurchasingDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IPurchasingDbContext>(p => p.GetRequiredService<PurchasingDbContext>());

        return services;
    }
}

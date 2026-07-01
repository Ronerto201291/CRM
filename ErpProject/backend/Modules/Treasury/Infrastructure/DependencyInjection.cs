using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Modules.Treasury.Infrastructure.Jobs;
using Erp.Modules.Treasury.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Treasury.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddTreasuryInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<TreasuryDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ITreasuryDbContext>(p => p.GetRequiredService<TreasuryDbContext>());

        // Servicios de dominio
        services.AddScoped<BankReconciliationService>();

        // Exchange rate provider (ECB) + service
        services.AddHttpClient<EcbExchangeRateProvider>();
        services.AddScoped<IExchangeRateProvider, EcbExchangeRateProvider>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddHostedService<ExchangeRateRefreshJob>();

        return services;
    }
}

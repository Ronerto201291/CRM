using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Application.Validators;
using FluentValidation;
using Erp.Modules.Treasury.Infrastructure.Data;
using Erp.Modules.Treasury.Infrastructure.Jobs;
using Erp.Modules.Treasury.Infrastructure.Services;
using Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;
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

        services.AddDbContext<TreasuryDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
               .AddInterceptors(sp.GetRequiredService<Erp.Infrastructure.Interceptors.AuditSaveChangesInterceptor>())
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<ITreasuryDbContext>(p => p.GetRequiredService<TreasuryDbContext>());

        // Servicios de dominio
        services.AddScoped<BankReconciliationService>();
        services.AddScoped<IBankReconciliationService>(sp => sp.GetRequiredService<BankReconciliationService>());
        services.AddScoped<ISepaXmlGenerator, SepaXmlGenerator>();

        services.AddOpenBankingProvider(configuration);

        services.AddScoped<IExchangeRateLookup, ExchangeRateLookupAdapter>();

        // Exchange rate provider (ECB) + service
        services.AddHttpClient<EcbExchangeRateProvider>();
        services.AddScoped<IExchangeRateProvider, EcbExchangeRateProvider>();
        services.AddScoped<IExchangeRateService, ExchangeRateService>();
        services.AddHostedService<ExchangeRateRefreshJob>();
        services.AddValidatorsFromAssembly(typeof(CreateBankAccountCommandValidator).Assembly);

        return services;
    }
}

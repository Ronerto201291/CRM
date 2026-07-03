using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Infrastructure.Data;
using Erp.Modules.Accounting.Infrastructure.Jobs;
using Erp.Modules.Accounting.Infrastructure.Services;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Accounting.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddAccountingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<AccountingDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IAccountingDbContext>(p => p.GetRequiredService<AccountingDbContext>());

        services.AddScoped<IAgingDataService, AgingDataService>();
        services.AddScoped<IIvaRegisterDataService, IvaRegisterDataService>();
        services.AddScoped<IAeatModelsDataService, AeatModelsDataService>();
        services.AddScoped<IRecargoInvoiceReader, RecargoInvoiceReader>();

        // Hangfire jobs (transient — Hangfire resolves per execution)
        services.AddTransient<AmortizationMonthlyJob>();
        services.AddTransient<DeferredEntryMonthlyJob>();

        return services;
    }

    /// <summary>
    /// Registra los jobs recurrentes de contabilidad en Hangfire.
    /// Llamar desde Program.cs DESPUÉS de app.UseHangfireDashboard().
    /// </summary>
    public static void RegisterAccountingRecurringJobs()
    {
        // Dotación amortización: el día 1 de cada mes a las 02:00 UTC
        RecurringJob.AddOrUpdate<AmortizationMonthlyJob>(
            "accounting-amortization-monthly",
            job => job.RunAsync(CancellationToken.None),
            "0 2 1 * *");

        // Reconocimiento periodificaciones: el día 1 de cada mes a las 02:30 UTC
        RecurringJob.AddOrUpdate<DeferredEntryMonthlyJob>(
            "accounting-deferred-entry-monthly",
            job => job.RunAsync(CancellationToken.None),
            "30 2 1 * *");
    }
}

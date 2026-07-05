using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Application.Services;
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

        services.AddDbContext<AccountingDbContext>((sp, options) =>
            options.UseNpgsql(connectionString)
               .AddInterceptors(sp.GetRequiredService<Erp.Infrastructure.Interceptors.AuditSaveChangesInterceptor>())
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IAccountingDbContext>(p => p.GetRequiredService<AccountingDbContext>());
        services.AddScoped<IBankReconciliationLedgerQuery, BankReconciliationLedgerQuery>();
        services.AddScoped<IPayrollJournalEntryGenerator, PayrollJournalEntryGenerator>();
        services.AddScoped<IConsolidationMetricsQuery, ConsolidationMetricsQuery>();
        services.AddScoped<AccountingService>();
        services.AddScoped<IRecargoInvoiceReader, RecargoInvoiceReader>();
        services.AddScoped<ILibroIvaEmitidasExporter, LibroIvaEmitidasExporter>();
        services.AddScoped<ILibroIvaRecibidasExporter, LibroIvaRecibidasExporter>();
        services.AddScoped<IModelo347Exporter, Modelo347Exporter>();
        services.AddScoped<IModelo347Reader, Modelo347Reader>();
        services.AddScoped<IAgingReportReader, AgingReportReader>();
        services.AddScoped<IModelo303Exporter, Modelo303Exporter>();
        services.AddScoped<IModelo111Reader, Modelo111Reader>();
        services.AddScoped<IModelo190Reader, Modelo190Reader>();
        services.AddScoped<IModelo390Exporter, Modelo390Exporter>();
        services.AddScoped<IModelo349Exporter, Modelo349Exporter>();
        services.AddScoped<IModelo303Reader, Modelo303Reader>();
        services.AddScoped<IModelo303XmlExporter, Modelo303XmlExporter>();
        services.AddScoped<IFiscalSkeletonXmlExporter, FiscalSkeletonXmlExporter>();
        services.AddScoped<IModelo390XmlExporter, Modelo390XmlExporter>();

        // Hangfire jobs (transient — Hangfire resolves per execution)
        services.AddTransient<AmortizationMonthlyJob>();
        services.AddTransient<DeferredEntryMonthlyJob>();
        services.AddTransient<AccountantExportJob>();

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

        RecurringJob.AddOrUpdate<AccountantExportJob>(
            "accountant-export-monthly",
            job => job.ExecuteAsync(CancellationToken.None),
            "0 6 3 * *");
    }
}

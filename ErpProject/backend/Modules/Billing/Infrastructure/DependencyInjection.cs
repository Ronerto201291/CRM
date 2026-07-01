using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Billing.Infrastructure.Services;
using Erp.Infrastructure.Services.Sii;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Billing.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddBillingInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("DefaultConnection missing.");

        services.AddDbContext<BillingDbContext>(options =>
            options.UseNpgsql(connectionString)
               .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

        services.AddScoped<IBillingDbContext>(p => p.GetRequiredService<BillingDbContext>());

        // PDF de facturas (QuestPDF, singleton thread-safe)
        services.AddSingleton<IInvoicePdfService, InvoicePdfService>();

        // PDF de presupuestos (QuestPDF, singleton thread-safe)
        services.AddSingleton<IQuotePdfService, QuotePdfService>();

        // FacturaE 3.2.2 (Ley 18/2022 Crea y Crece)
        services.AddScoped<IFacturaEService, FacturaEService>();

        // VERI*FACTU: XML generator (bridge from Application to Infrastructure impl)
        services.AddScoped<IVerifactuXmlGenerator, VerifactuXmlGeneratorBridge>();

        // Job de expiración de presupuestos (Hangfire lo resuelve del DI)
        services.AddScoped<ExpireQuotesJob>();

        // VERI*FACTU submission job (Hangfire)
        services.AddScoped<VerifactuSubmissionJob>();
        services.AddScoped<IVerifactuSubmissionGateway, VerifactuSubmissionGateway>();

        return services;
    }
}

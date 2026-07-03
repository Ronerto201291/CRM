using Erp.Application.Common.Interfaces;
using Erp.Modules.Billing.Application.Interfaces;
using Erp.Modules.Billing.Infrastructure.Data;
using Erp.Modules.Billing.Infrastructure.Services;
using Erp.Infrastructure.Services.FacturaE;
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

        services.AddSingleton<IInvoicePdfService, InvoicePdfService>();
        services.AddSingleton<IQuotePdfService, QuotePdfService>();

        services.AddScoped<FacturaESigningService>();
        services.AddScoped<IFacturaEService, FacturaEService>();
        services.AddScoped<IFaceSubmissionService, FaceSubmissionService>();
        services.AddHttpClient("Face", client => client.Timeout = TimeSpan.FromSeconds(60));

        services.AddScoped<IVerifactuXmlGenerator, VerifactuXmlGeneratorBridge>();
        services.AddScoped<ExpireQuotesJob>();
        services.AddScoped<VerifactuSubmissionJob>();
        services.AddScoped<IVerifactuSubmissionGateway, VerifactuSubmissionGateway>();

        return services;
    }
}

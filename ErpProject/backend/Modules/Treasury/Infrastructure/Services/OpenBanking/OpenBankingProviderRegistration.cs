using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Application.Options;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;

/// <summary>
/// Selección del proveedor PSD2 (ADR-0018 #28): Mock en dev sin credenciales;
/// Configurable cuando hay ClientId/Secret/ApiBaseUrl; Stub en prod sin contrato.
/// </summary>
public static class OpenBankingProviderRegistration
{
    public static void AddOpenBankingProvider(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<OpenBankingOptions>(
            configuration.GetSection(OpenBankingOptions.SectionName));

        var opts = configuration.GetSection(OpenBankingOptions.SectionName).Get<OpenBankingOptions>()
            ?? new OpenBankingOptions();

        var provider = opts.Provider?.Trim() ?? "Mock";
        var env = configuration["ASPNETCORE_ENVIRONMENT"] ?? "Production";
        var isDevLike = string.Equals(env, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(env, "IntegrationTests", StringComparison.OrdinalIgnoreCase);

        var hasCredentials = !string.IsNullOrWhiteSpace(opts.ClientId)
            && !string.IsNullOrWhiteSpace(opts.ClientSecret)
            && !string.IsNullOrWhiteSpace(opts.ApiBaseUrl);

        var useConfigurable = hasCredentials
            || IsNamedConfigurableProvider(provider);

        if (useConfigurable)
        {
            services.AddHttpClient(nameof(ConfigurableOpenBankingProvider));
            services.AddHttpClient(nameof(ConfigurableOpenBankingProvider) + "-token");
            services.AddScoped<IOpenBankingProvider, ConfigurableOpenBankingProvider>();
            return;
        }

        if (string.Equals(provider, "Stub", StringComparison.OrdinalIgnoreCase))
        {
            services.AddScoped<IOpenBankingProvider, StubOpenBankingProvider>();
            return;
        }

        if (isDevLike)
        {
            services.AddScoped<IOpenBankingProvider, MockOpenBankingProvider>();
            return;
        }

        services.AddScoped<IOpenBankingProvider, StubOpenBankingProvider>();
    }

    public static bool IsNamedConfigurableProvider(string provider) =>
        string.Equals(provider, "GoCardless", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Nordigen", StringComparison.OrdinalIgnoreCase)
        || string.Equals(provider, "Configurable", StringComparison.OrdinalIgnoreCase);
}

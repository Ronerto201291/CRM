using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;

/// <summary>
/// Stub de producción sin contrato bancario: no importa movimientos hasta configurar un proveedor real.
/// </summary>
public sealed class StubOpenBankingProvider(
    IOptions<OpenBankingOptions> options,
    ILogger<StubOpenBankingProvider> log) : IOpenBankingProvider
{
    public string ProviderName => "Stub";

    public Task<IReadOnlyList<OpenBankingTransaction>> FetchTransactionsAsync(
        OpenBankingAccountContext account,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        log.LogWarning(
            "OpenBanking Stub activo (Provider={Provider}, ApiBaseUrl={ApiBaseUrl}). " +
            "Configure un proveedor PSD2 real y credenciales ClientId/ClientSecret.",
            options.Value.Provider, options.Value.ApiBaseUrl);
        return Task.FromResult<IReadOnlyList<OpenBankingTransaction>>([]);
    }
}

using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Erp.Modules.Treasury.Infrastructure.Services.OpenBanking;

/// <summary>
/// Proveedor PSD2 configurable: intenta OAuth client_credentials contra la API configurada
/// cuando hay credenciales; si no, devuelve lista vacía (sin simular movimientos falsos).
/// Compatible con GoCardless Bank Account Data (ex-Nordigen). Ver docs/open-banking-psd2.md.
/// </summary>
public sealed class ConfigurableOpenBankingProvider(
    IOptions<OpenBankingOptions> options,
    IHttpClientFactory httpClientFactory,
    ILogger<ConfigurableOpenBankingProvider> log) : IOpenBankingProvider
{
    public string ProviderName => options.Value.Provider;

    public async Task<IReadOnlyList<OpenBankingTransaction>> FetchTransactionsAsync(
        OpenBankingAccountContext account,
        DateTime? fromUtc,
        DateTime? toUtc,
        CancellationToken ct = default)
    {
        var opts = options.Value;
        if (string.IsNullOrWhiteSpace(opts.ClientId) || string.IsNullOrWhiteSpace(opts.ApiBaseUrl))
        {
            log.LogWarning(
                "OpenBanking {Provider}: credenciales o ApiBaseUrl no configurados. " +
                "Configure OpenBanking:ClientId, ClientSecret y ApiBaseUrl para activar PSD2 real.",
                opts.Provider);
            return [];
        }

        if (string.IsNullOrWhiteSpace(account.ExternalAccountId))
        {
            log.LogWarning("OpenBanking: cuenta {BankAccountId} sin ExternalAccountId (requisito del proveedor).", account.BankAccountId);
            return [];
        }

        try
        {
            var token = await ObtainAccessTokenAsync(opts, ct);
            if (string.IsNullOrEmpty(token))
                return [];

            var client = httpClientFactory.CreateClient(nameof(ConfigurableOpenBankingProvider));
            client.BaseAddress = new Uri(opts.ApiBaseUrl.TrimEnd('/') + "/");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var from = (fromUtc ?? DateTime.UtcNow.AddDays(-30)).ToString("yyyy-MM-dd");
            var to = (toUtc ?? DateTime.UtcNow).ToString("yyyy-MM-dd");
            var url = $"accounts/{account.ExternalAccountId}/transactions/?date_from={from}&date_to={to}";

            using var response = await client.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                log.LogWarning("OpenBanking API {Status} al obtener transacciones de {AccountId}.",
                    response.StatusCode, account.ExternalAccountId);
                return [];
            }

            await using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            return ParseTransactions(doc);
        }
        catch (Exception ex)
        {
            log.LogError(ex, "OpenBanking {Provider}: error al sincronizar cuenta {AccountId}.", opts.Provider, account.BankAccountId);
            return [];
        }
    }

    private async Task<string?> ObtainAccessTokenAsync(OpenBankingOptions opts, CancellationToken ct)
    {
        var client = httpClientFactory.CreateClient(nameof(ConfigurableOpenBankingProvider) + "-token");
        client.BaseAddress = new Uri(opts.ApiBaseUrl.TrimEnd('/') + "/");

        var payload = new
        {
            secret_id = opts.ClientId,
            secret_key = opts.ClientSecret,
        };

        using var response = await client.PostAsJsonAsync("token/new/", payload, ct);
        if (!response.IsSuccessStatusCode)
        {
            log.LogWarning("OpenBanking OAuth token/new devolvió {Status}.", response.StatusCode);
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        if (doc.RootElement.TryGetProperty("access", out var access))
            return access.GetString();
        if (doc.RootElement.TryGetProperty("access_token", out var accessToken))
            return accessToken.GetString();

        log.LogWarning("OpenBanking OAuth: respuesta sin access/access_token.");
        return null;
    }

    private static IReadOnlyList<OpenBankingTransaction> ParseTransactions(JsonDocument doc)
    {
        if (!doc.RootElement.TryGetProperty("transactions", out var transactionsRoot))
            return [];

        if (!transactionsRoot.TryGetProperty("booked", out var booked) || booked.ValueKind != JsonValueKind.Array)
            return [];

        var list = new List<OpenBankingTransaction>();
        foreach (var item in booked.EnumerateArray())
        {
            var amount = 0m;
            if (item.TryGetProperty("transactionAmount", out var amtObj)
                && amtObj.TryGetProperty("amount", out var amtStr)
                && decimal.TryParse(amtStr.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var parsed))
                amount = parsed;

            var date = DateTime.UtcNow;
            if (item.TryGetProperty("bookingDate", out var bd) && DateTime.TryParse(bd.GetString(), out var d))
                date = DateTime.SpecifyKind(d, DateTimeKind.Utc);

            var desc = item.TryGetProperty("remittanceInformationUnstructured", out var ri)
                ? ri.GetString() ?? ""
                : "";

            var extId = item.TryGetProperty("transactionId", out var tid)
                ? tid.GetString() ?? Guid.NewGuid().ToString("N")
                : Guid.NewGuid().ToString("N");

            list.Add(new OpenBankingTransaction(extId, date, amount, desc, null));
        }

        return list;
    }
}

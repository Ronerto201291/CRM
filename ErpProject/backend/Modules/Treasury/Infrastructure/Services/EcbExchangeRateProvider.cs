using System.Text.RegularExpressions;
using Erp.Modules.Treasury.Application.Interfaces;

namespace Erp.Modules.Treasury.Infrastructure.Services;

public class EcbExchangeRateProvider : IExchangeRateProvider
{
    private readonly HttpClient _httpClient;
    private const string EcbUrl = "https://www.ecb.europa.eu/stats/service/ecb.refRates/data/USD.xml?lang=en";

    public string ProviderName => "ECB";

    public EcbExchangeRateProvider(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<ExchangeRateResult> GetRatesAsync(CancellationToken ct = default)
    {
        // ECB publishes rates against EUR; we use USD as intermediate base
        var response = await _httpClient.GetStringAsync(EcbUrl, ct);

        var rates = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase)
        {
            ["EUR"] = 1.0m
        };

        // Parse: <Obs><ObsDimension VALUE="USD" /><ObsValue value="1.0856"/></Obs>
        var matches = Regex.Matches(response, @"<Obs[^>]*>.*?<ObsDimension\s+VALUE=""([^""]+)""[^>]*/>.*?<ObsValue\s+value=""([^""]+)""", RegexOptions.Singleline);
        foreach (Match m in matches)
        {
            var code = m.Groups[1].Value;
            if (decimal.TryParse(m.Groups[2].Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var rate))
            {
                rates[code] = rate;
            }
        }

        // Try to get the period/date from the generic: <html:span class="ecb-repeatPeriod">18 May 2026</html:span>
        DateTime rateDate = DateTime.UtcNow;
        var periodMatch = Regex.Match(response, @"ecb-repeatPeriod[^>]*>([^<]+)</", RegexOptions.Singleline);
        if (periodMatch.Success && DateTime.TryParse(periodMatch.Groups[1].Value.Trim(), out var parsed))
        {
            rateDate = parsed;
        }

        return new ExchangeRateResult(rateDate, "EUR", rates);
    }
}

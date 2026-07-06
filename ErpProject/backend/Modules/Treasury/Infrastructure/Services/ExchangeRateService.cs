using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Infrastructure.Services;

public class ExchangeRateService : IExchangeRateService
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;
    private readonly IExchangeRateProvider _provider;
    private readonly ILogger<ExchangeRateService> _logger;

    public ExchangeRateService(
        ITreasuryDbContext ctx,
        ITenantContext tenantContext,
        IExchangeRateProvider provider,
        ILogger<ExchangeRateService> logger)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
        _provider = provider;
        _logger = logger;
    }

    public async Task<decimal> GetRateAsync(string from, string to, CancellationToken ct = default)
    {
        if (from.Equals(to, StringComparison.OrdinalIgnoreCase)) return 1m;

        var tenantId = _tenantContext.TenantId;
        if (tenantId == null) return 1m;

        var currencies = await _ctx.Currencies
            .Where(c => c.CompanyId == tenantId && c.IsActive)
            .ToDictionaryAsync(c => c.Code.ToUpperInvariant(), c => c.ExchangeRate, ct);

        if (currencies.TryGetValue(from.ToUpperInvariant(), out var fromRate) &&
            currencies.TryGetValue(to.ToUpperInvariant(), out var toRate))
        {
            return toRate / fromRate;
        }

        return 1m;
    }

    public async Task RefreshRatesAsync(CancellationToken ct = default)
    {
        var tenantId = _tenantContext.TenantId;
        if (tenantId == null)
        {
            _logger.LogWarning("RefreshRatesAsync skipped: no tenant context (use RefreshAllTenantsRatesAsync from background jobs).");
            return;
        }

        await RefreshRatesForCompanyAsync(tenantId.Value, ct);
    }

    public async Task RefreshAllTenantsRatesAsync(CancellationToken ct = default)
    {
        try
        {
            var companyIds = await _ctx.Currencies
                .IgnoreQueryFilters()
                .Where(c => c.IsActive)
                .Select(c => c.CompanyId)
                .Distinct()
                .ToListAsync(ct);

            if (companyIds.Count == 0)
            {
                _logger.LogInformation("ExchangeRateRefreshJob: no companies with active currencies.");
                return;
            }

            var result = await _provider.GetRatesAsync(ct);

            foreach (var companyId in companyIds)
                await ApplyRatesToCompanyAsync(companyId, result, ct);

            _logger.LogInformation("Exchange rates refreshed for {Count} companies from {Provider}",
                companyIds.Count, _provider.ProviderName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh exchange rates for all tenants");
        }
    }

    private async Task RefreshRatesForCompanyAsync(Guid companyId, CancellationToken ct)
    {
        var result = await _provider.GetRatesAsync(ct);
        await ApplyRatesToCompanyAsync(companyId, result, ct);
    }

    private async Task ApplyRatesToCompanyAsync(Guid companyId, ExchangeRateResult result, CancellationToken ct)
    {
        var currencies = await _ctx.Currencies
            .IgnoreQueryFilters()
            .Where(c => c.CompanyId == companyId && c.IsActive)
            .ToListAsync(ct);

        foreach (var currency in currencies)
        {
            if (!result.Rates.TryGetValue(currency.Code.ToUpperInvariant(), out var rate))
                continue;

            currency.ExchangeRate = rate;
            currency.RateDate = result.Date;
            currency.Source = _provider.ProviderName;
            currency.UpdatedAt = DateTime.UtcNow;

            _ctx.ExchangeRateHistories.Add(new ExchangeRateHistory
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                CurrencyCode = currency.Code,
                Rate = rate,
                RateDate = result.Date,
                Source = _provider.ProviderName,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
            });
        }

        await _ctx.SaveChangesAsync(ct);
    }
}

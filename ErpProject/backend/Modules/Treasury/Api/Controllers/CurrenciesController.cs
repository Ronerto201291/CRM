using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Erp.Modules.Treasury.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Api.Controllers;

[ApiController]
[Route("api/v1/treasury/currencies")]
[Authorize]
public class CurrenciesController : ControllerBase
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenantContext;
    private readonly IExchangeRateService _rateService;

    public CurrenciesController(ITreasuryDbContext ctx, ITenantContext tenantContext, IExchangeRateService rateService)
    {
        _ctx = ctx;
        _tenantContext = tenantContext;
        _rateService = rateService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var currencies = await _ctx.Currencies
            .Where(c => c.CompanyId == tenantId && c.IsActive)
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .ToListAsync(ct);
        return Ok(currencies.Select(c => new
        {
            c.Id,
            c.Code,
            c.Name,
            c.ExchangeRate,
            c.RateDate,
            c.Source
        }));
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCurrencyDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var existing = await _ctx.Currencies
            .AnyAsync(c => c.CompanyId == tenantId && c.Code == dto.Code.ToUpperInvariant(), ct);
        if (existing)
            return BadRequest(new { error = $"Currency {dto.Code} already exists for this company" });

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Code = dto.Code.ToUpperInvariant(),
            Name = dto.Name,
            ExchangeRate = dto.ExchangeRate,
            RateDate = DateTime.UtcNow,
            Source = "Manual",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _ctx.Currencies.Add(currency);
        await _ctx.SaveChangesAsync(ct);

        return CreatedAtAction(nameof(GetAll), new { id = currency.Id }, new
        {
            currency.Id,
            currency.Code,
            currency.Name,
            currency.ExchangeRate,
            currency.RateDate,
            currency.Source
        });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCurrencyRateDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var currency = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == tenantId, ct);
        if (currency == null)
            return NotFound(new { error = "Currency not found" });

        currency.ExchangeRate = dto.ExchangeRate;
        currency.RateDate = DateTime.UtcNow;
        currency.UpdatedAt = DateTime.UtcNow;

        await _ctx.SaveChangesAsync(ct);
        return Ok(new
        {
            currency.Id,
            currency.Code,
            currency.Name,
            currency.ExchangeRate,
            currency.RateDate
        });
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var currency = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.Id == id && c.CompanyId == tenantId, ct);
        if (currency == null)
            return NotFound(new { error = "Currency not found" });

        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return NoContent();
    }

    [HttpGet("rates")]
    public async Task<IActionResult> GetRates(CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");
        var currencies = await _ctx.Currencies
            .Where(c => c.CompanyId == tenantId && c.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);
        var rates = currencies.ToDictionary(c => c.Code, c => c.ExchangeRate);
        return Ok(new { rates });
    }

    [HttpPost("exchange")]
    public async Task<IActionResult> ExchangeCurrency([FromBody] ExchangeCurrencyDto dto, CancellationToken ct)
    {
        var tenantId = _tenantContext.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var from = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.CompanyId == tenantId && c.Code == dto.FromCurrency.ToUpperInvariant() && c.IsActive, ct);
        var to = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.CompanyId == tenantId && c.Code == dto.ToCurrency.ToUpperInvariant() && c.IsActive, ct);

        if (from == null) return BadRequest(new { error = $"Currency {dto.FromCurrency} not found" });
        if (to == null) return BadRequest(new { error = $"Currency {dto.ToCurrency} not found" });

        decimal rate;
        if (dto.Source == "Automatic")
        {
            rate = await _rateService.GetRateAsync(dto.FromCurrency, dto.ToCurrency, ct);
        }
        else
        {
            rate = dto.Amount / from.ExchangeRate * to.ExchangeRate;
        }

        var exchange = new CurrencyExchange
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            FromCurrency = from.Code,
            ToCurrency = to.Code,
            Amount = dto.Amount,
            ExchangedAmount = rate,
            ExchangeRate = to.ExchangeRate / from.ExchangeRate,
            ExchangeDate = DateTime.UtcNow,
            Type = dto.Source == "Automatic" ? "Automatic" : "Manual",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _ctx.CurrencyExchanges.Add(exchange);
        await _ctx.SaveChangesAsync(ct);

        return Ok(new { exchangedAmount = rate, rate = exchange.ExchangeRate });
    }
}

public record CreateCurrencyDto(string Code, string Name, decimal ExchangeRate);
public record UpdateCurrencyRateDto(decimal ExchangeRate);
public record ExchangeCurrencyDto(string FromCurrency, string ToCurrency, decimal Amount, string Source = "Manual");
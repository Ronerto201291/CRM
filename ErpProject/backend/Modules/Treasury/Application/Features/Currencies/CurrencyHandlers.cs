using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Currencies;

// ── Queries / Commands ───────────────────────────────────────────────────────

public record GetCurrenciesQuery : IRequest<IReadOnlyList<CurrencyDto>>;

public record GetCurrencyRatesQuery : IRequest<Dictionary<string, decimal>>;

public record CreateCurrencyCommand(string Code, string Name, decimal ExchangeRate) : IRequest<CurrencyDto>;

public record UpdateCurrencyRateCommand(Guid Id, decimal ExchangeRate) : IRequest<CurrencyDto>;

public record DeleteCurrencyCommand(Guid Id) : IRequest<bool>;

public record ExchangeCurrencyCommand(
    string FromCurrency,
    string ToCurrency,
    decimal Amount,
    string Source = "Manual") : IRequest<ExchangeCurrencyResult>;

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record CurrencyDto(
    Guid Id,
    string Code,
    string Name,
    decimal ExchangeRate,
    DateTime RateDate,
    string Source);

public record ExchangeCurrencyResult(decimal ExchangedAmount, decimal Rate);

// ── Handlers ─────────────────────────────────────────────────────────────────

public class GetCurrenciesHandler : IRequestHandler<GetCurrenciesQuery, IReadOnlyList<CurrencyDto>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCurrenciesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<IReadOnlyList<CurrencyDto>> Handle(GetCurrenciesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        return await _ctx.Currencies
            .Where(c => c.CompanyId == tenantId && c.IsActive)
            .AsNoTracking()
            .OrderBy(c => c.Code)
            .Select(c => new CurrencyDto(c.Id, c.Code, c.Name, c.ExchangeRate, c.RateDate, c.Source))
            .ToListAsync(ct);
    }
}

public class GetCurrencyRatesHandler : IRequestHandler<GetCurrencyRatesQuery, Dictionary<string, decimal>>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GetCurrencyRatesHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<Dictionary<string, decimal>> Handle(GetCurrencyRatesQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var currencies = await _ctx.Currencies
            .Where(c => c.CompanyId == tenantId && c.IsActive)
            .AsNoTracking()
            .ToListAsync(ct);
        return currencies.ToDictionary(c => c.Code, c => c.ExchangeRate);
    }
}

public class CreateCurrencyHandler : IRequestHandler<CreateCurrencyCommand, CurrencyDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public CreateCurrencyHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CurrencyDto> Handle(CreateCurrencyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var code = request.Code.ToUpperInvariant();

        var exists = await _ctx.Currencies
            .AnyAsync(c => c.CompanyId == tenantId && c.Code == code, ct);
        if (exists)
            throw new InvalidOperationException($"Currency {code} already exists for this company");

        var currency = new Currency
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            Code = code,
            Name = request.Name,
            ExchangeRate = request.ExchangeRate,
            RateDate = DateTime.UtcNow,
            Source = "Manual",
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _ctx.Currencies.Add(currency);
        await _ctx.SaveChangesAsync(ct);

        return new CurrencyDto(currency.Id, currency.Code, currency.Name, currency.ExchangeRate, currency.RateDate, currency.Source);
    }
}

public class UpdateCurrencyRateHandler : IRequestHandler<UpdateCurrencyRateCommand, CurrencyDto>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public UpdateCurrencyRateHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CurrencyDto> Handle(UpdateCurrencyRateCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var currency = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.CompanyId == tenantId, ct)
            ?? throw new KeyNotFoundException("Currency not found");

        currency.ExchangeRate = request.ExchangeRate;
        currency.RateDate = DateTime.UtcNow;
        currency.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);

        return new CurrencyDto(currency.Id, currency.Code, currency.Name, currency.ExchangeRate, currency.RateDate, currency.Source);
    }
}

public class DeleteCurrencyHandler : IRequestHandler<DeleteCurrencyCommand, bool>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;

    public DeleteCurrencyHandler(ITreasuryDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<bool> Handle(DeleteCurrencyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var currency = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.Id == request.Id && c.CompanyId == tenantId, ct);
        if (currency is null) return false;

        currency.IsActive = false;
        currency.UpdatedAt = DateTime.UtcNow;
        await _ctx.SaveChangesAsync(ct);
        return true;
    }
}

public class ExchangeCurrencyHandler : IRequestHandler<ExchangeCurrencyCommand, ExchangeCurrencyResult>
{
    private readonly ITreasuryDbContext _ctx;
    private readonly ITenantContext _tenant;
    private readonly IExchangeRateService _rateService;

    public ExchangeCurrencyHandler(ITreasuryDbContext ctx, ITenantContext tenant, IExchangeRateService rateService)
    {
        _ctx = ctx;
        _tenant = tenant;
        _rateService = rateService;
    }

    public async Task<ExchangeCurrencyResult> Handle(ExchangeCurrencyCommand request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");

        var from = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.CompanyId == tenantId && c.Code == request.FromCurrency.ToUpperInvariant() && c.IsActive, ct)
            ?? throw new InvalidOperationException($"Currency {request.FromCurrency} not found");

        var to = await _ctx.Currencies
            .FirstOrDefaultAsync(c => c.CompanyId == tenantId && c.Code == request.ToCurrency.ToUpperInvariant() && c.IsActive, ct)
            ?? throw new InvalidOperationException($"Currency {request.ToCurrency} not found");

        decimal exchangedAmount;
        decimal rate;
        if (request.Source == "Automatic")
        {
            exchangedAmount = await _rateService.GetRateAsync(request.FromCurrency, request.ToCurrency, ct);
            rate = to.ExchangeRate / from.ExchangeRate;
        }
        else
        {
            rate = to.ExchangeRate / from.ExchangeRate;
            exchangedAmount = request.Amount / from.ExchangeRate * to.ExchangeRate;
        }

        var exchange = new CurrencyExchange
        {
            Id = Guid.NewGuid(),
            CompanyId = tenantId,
            FromCurrency = from.Code,
            ToCurrency = to.Code,
            Amount = request.Amount,
            ExchangedAmount = exchangedAmount,
            ExchangeRate = rate,
            ExchangeDate = DateTime.UtcNow,
            Type = request.Source == "Automatic" ? "Automatic" : "Manual",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        _ctx.CurrencyExchanges.Add(exchange);
        await _ctx.SaveChangesAsync(ct);

        return new ExchangeCurrencyResult(exchangedAmount, rate);
    }
}

using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Treasury.Application.Features.Treasury.Handlers;

public record LiquidityHorizonDto(
    int Days,
    DateTime HorizonDate,
    decimal ExpectedInflow,
    decimal ExpectedOutflow,
    decimal RecurringInflow,
    decimal RecurringOutflow,
    decimal ProjectedBalance);

public record TreasuryLiquidityForecastDto(
    decimal CurrentBankBalance,
    IReadOnlyList<LiquidityHorizonDto> Horizons,
    string? AiInsight = null);

public record GetTreasuryLiquidityForecastQuery : IRequest<TreasuryLiquidityForecastDto>;

/// <summary>Previsión heurística 30/60/90 días (#40) desde cobros/pagos pendientes + recurrentes + saldo bancario.</summary>
public sealed class GetTreasuryLiquidityForecastHandler
    : IRequestHandler<GetTreasuryLiquidityForecastQuery, TreasuryLiquidityForecastDto>
{
    private readonly ITreasuryDbContext _treasury;
    private readonly ITenantContext _tenant;
    private readonly IAutomationBillingQuery _billing;
    private readonly IAutomationExpensesQuery _expenses;
    private readonly IAutomationRecurringQuery _recurring;
    private readonly IExpenseAiAssistant? _ai;

    public GetTreasuryLiquidityForecastHandler(
        ITreasuryDbContext treasury,
        ITenantContext tenant,
        IAutomationBillingQuery billing,
        IAutomationExpensesQuery expenses,
        IAutomationRecurringQuery recurring,
        IExpenseAiAssistant? ai = null)
    {
        _treasury = treasury;
        _tenant = tenant;
        _billing = billing;
        _expenses = expenses;
        _recurring = recurring;
        _ai = ai;
    }

    public async Task<TreasuryLiquidityForecastDto> Handle(GetTreasuryLiquidityForecastQuery request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant not resolved");
        var today = DateTime.UtcNow.Date;

        var bankBalance = await _treasury.BankAccounts
            .Where(a => a.IsActive)
            .SumAsync(a => a.CurrentBalance, ct);

        var monthlyIn = await _recurring.GetMonthlyRecurringInflowAsync(companyId, ct);
        var monthlyOut = await _recurring.GetMonthlyRecurringOutflowAsync(companyId, ct);

        var horizons = new List<LiquidityHorizonDto>();
        decimal cumulativeNet = 0m;

        foreach (var days in new[] { 30, 60, 90 })
        {
            var horizon = today.AddDays(days);
            var receivables = await _billing.GetPendingReceivablesAsync(companyId, horizon, ct);
            var payables = await _expenses.GetPendingPayablesAsync(companyId, horizon, ct);

            var inflow = receivables.Sum(i => i.Total);
            var outflow = payables.Sum(e => e.Total);
            var recIn = monthlyIn * (days / 30m);
            var recOut = monthlyOut * (days / 30m);
            cumulativeNet += inflow - outflow + recIn - recOut;

            horizons.Add(new LiquidityHorizonDto(
                days, horizon, inflow, outflow, recIn, recOut,
                Math.Round(bankBalance + cumulativeNet, 2)));
        }

        string? aiInsight = null;
        if (_ai?.IsEnabled == true)
        {
            aiInsight = await _ai.SummarizeLiquidityForecastAsync(
                Math.Round(bankBalance, 2),
                horizons.Select(h => (h.Days, h.ProjectedBalance)).ToList(),
                ct);
        }

        return new TreasuryLiquidityForecastDto(Math.Round(bankBalance, 2), horizons, aiInsight);
    }
}

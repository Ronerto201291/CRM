using Erp.Application.Common.Interfaces;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.FinancialStatements;

public record GenerateCashFlowStatementQuery(int Year, int Month) : IRequest<CashFlowStatementDto>;

public record CashFlowStatementDto(
    Guid Id,
    decimal OperatingCashFlow,
    decimal InvestingCashFlow,
    decimal FinancingCashFlow,
    decimal NetCashFlow,
    string Period,
    string Status,
    string? Note);

public class GenerateCashFlowStatementHandler : IRequestHandler<GenerateCashFlowStatementQuery, CashFlowStatementDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GenerateCashFlowStatementHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CashFlowStatementDto> Handle(GenerateCashFlowStatementQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var periodStart = new DateTime(request.Year, request.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var periodEnd = periodStart.AddMonths(1);

        var lines = await (
            from l in _ctx.JournalEntryLines
            join je in _ctx.JournalEntries on l.JournalEntryId equals je.Id
            where je.CompanyId == tenantId && je.IsPosted && je.Date >= periodStart && je.Date < periodEnd
            select new { l.JournalEntryId, l.AccountCode, l.Debit, l.Credit }
        ).AsNoTracking().ToListAsync(ct);

        if (lines.Count == 0)
        {
            return new CashFlowStatementDto(
                Guid.NewGuid(), 0, 0, 0, 0,
                $"{request.Month:D2}/{request.Year}",
                "Empty",
                "Sin asientos contables en el período.");
        }

        decimal operating = 0, investing = 0, financing = 0;

        foreach (var entry in lines.GroupBy(l => l.JournalEntryId))
        {
            var cashDelta = entry
                .Where(l => l.AccountCode.StartsWith("57"))
                .Sum(l => l.Debit - l.Credit);
            if (cashDelta == 0) continue;

            var otherPrefixes = entry
                .Where(l => !l.AccountCode.StartsWith("57"))
                .Select(l => l.AccountCode.Length > 0 ? l.AccountCode[0] : '0')
                .Distinct()
                .ToList();

            if (otherPrefixes.Any(p => p == '2'))
                investing += cashDelta;
            else if (otherPrefixes.Any(p => p is '1' or '4' or '5'))
                financing += cashDelta;
            else
                operating += cashDelta;
        }

        var net = operating + investing + financing;

        return new CashFlowStatementDto(
            Guid.NewGuid(),
            Math.Round(operating, 2),
            Math.Round(investing, 2),
            Math.Round(financing, 2),
            Math.Round(net, 2),
            $"{request.Month:D2}/{request.Year}",
            "Calculated",
            "Calculado desde movimientos de tesorería (57*) y contrapartidas PGC.");
    }
}

public record GenerateEquityStatementQuery(int Year) : IRequest<EquityStatementDto>;

public record EquityStatementDto(
    Guid Id,
    decimal BeginningCapital,
    decimal NetIncome,
    decimal DividendsPaid,
    decimal OtherChanges,
    decimal EndingCapital,
    string Status,
    string? Note);

public class GenerateEquityStatementHandler : IRequestHandler<GenerateEquityStatementQuery, EquityStatementDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GenerateEquityStatementHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<EquityStatementDto> Handle(GenerateEquityStatementQuery request, CancellationToken ct)
    {
        var tenantId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");

        var lines = await (
            from l in _ctx.JournalEntryLines
            join je in _ctx.JournalEntries on l.JournalEntryId equals je.Id
            where je.CompanyId == tenantId && je.IsPosted && je.Date.Year == request.Year
                  && (l.AccountCode.StartsWith("10") || l.AccountCode.StartsWith("11"))
            select new { je.Date, l.AccountCode, l.Debit, l.Credit }
        ).AsNoTracking().ToListAsync(ct);

        var beginning = lines
            .Where(l => l.Date.Month < 7)
            .Sum(l => l.Credit - l.Debit);
        var netIncome = lines.Sum(l => l.Credit - l.Debit);
        var ending = netIncome;

        return new EquityStatementDto(
            Guid.NewGuid(),
            Math.Round(beginning, 2),
            Math.Round(netIncome - beginning, 2),
            0,
            0,
            Math.Round(ending, 2),
            lines.Count > 0 ? "Calculated" : "Empty",
            lines.Count > 0
                ? "Patrimonio neto (cuentas 10–11) desde mayor contable."
                : "Sin movimientos de patrimonio en el ejercicio.");
    }
}

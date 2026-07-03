using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Erp.Modules.Accounting.Application.Features.FinancialStatements;

public record GenerateCashFlowCommand(int FiscalYear) : IRequest<CashFlowResultDto>;
public record GenerateEquityStatementCommand(int FiscalYear) : IRequest<EquityResultDto>;

public record CashFlowResultDto(
    Guid Id,
    int FiscalYear,
    decimal OperatingActivitiesCash,
    decimal InvestingActivitiesCash,
    decimal FinancingActivitiesCash,
    decimal NetChangeInCash,
    decimal BeginningCash,
    decimal EndingCash,
    string Status);

public record EquityResultDto(
    Guid Id,
    int FiscalYear,
    decimal BeginningCapital,
    decimal NetIncome,
    decimal DividendsPaid,
    decimal OtherChanges,
    decimal EndingCapital,
    string Status);

public sealed class GenerateCashFlowHandler : IRequestHandler<GenerateCashFlowCommand, CashFlowResultDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GenerateCashFlowHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<CashFlowResultDto> Handle(GenerateCashFlowCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var from = new DateTime(request.FiscalYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var lines = await _ctx.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                        && l.JournalEntry.Date >= from
                        && l.JournalEntry.Date < to)
            .ToListAsync(ct);

        decimal operating = 0, investing = 0, financing = 0;
        var byEntry = lines.GroupBy(l => l.JournalEntryId);

        foreach (var group in byEntry)
        {
            var cashDelta = group
                .Where(l => IsCashAccount(l.Account?.Code))
                .Sum(l => l.Debit - l.Credit);
            if (cashDelta == 0) continue;

            var category = ClassifyCashFlow(group.Where(l => !IsCashAccount(l.Account?.Code)));
            switch (category)
            {
                case CashFlowCategory.Investing: investing += cashDelta; break;
                case CashFlowCategory.Financing: financing += cashDelta; break;
                default: operating += cashDelta; break;
            }
        }

        var beginning = await GetCashBalanceAtAsync(companyId, from, ct);
        var netChange = operating + investing + financing;
        var ending = beginning + netChange;

        var statement = new CashFlowStatement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FiscalYear = request.FiscalYear,
            OperatingActivitiesCash = Math.Round(operating, 2),
            InvestingActivitiesCash = Math.Round(investing, 2),
            FinancingActivitiesCash = Math.Round(financing, 2),
            NetChangeInCash = Math.Round(netChange, 2),
            BeginningCash = Math.Round(beginning, 2),
            EndingCash = Math.Round(ending, 2)
        };

        _ctx.CashFlowStatements.Add(statement);
        await _ctx.SaveChangesAsync(ct);

        return new CashFlowResultDto(
            statement.Id,
            statement.FiscalYear,
            statement.OperatingActivitiesCash,
            statement.InvestingActivitiesCash,
            statement.FinancingActivitiesCash,
            statement.NetChangeInCash,
            statement.BeginningCash,
            statement.EndingCash,
            "Generated");
    }

    private async Task<decimal> GetCashBalanceAtAsync(Guid companyId, DateTime date, CancellationToken ct)
    {
        return await _ctx.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                        && l.JournalEntry.Date < date
                        && IsCashAccount(l.Account!.Code))
            .SumAsync(l => l.Debit - l.Credit, ct);
    }

    private static bool IsCashAccount(string? code) =>
        code is not null && (code.StartsWith("570") || code.StartsWith("572"));

    private static CashFlowCategory ClassifyCashFlow(IEnumerable<JournalEntryLine> lines)
    {
        var codes = lines
            .Select(l => l.Account?.Code ?? "")
            .Where(c => !string.IsNullOrEmpty(c))
            .ToList();
        if (codes.Any(c => c.StartsWith('2') && !c.StartsWith("572")))
            return CashFlowCategory.Investing;
        if (codes.Any(c => c.StartsWith("10") || c.StartsWith("11") || c.StartsWith("17")
                           || c.StartsWith("50") || c.StartsWith("52") || c.StartsWith('5')))
            return CashFlowCategory.Financing;
        return CashFlowCategory.Operating;
    }

    private enum CashFlowCategory { Operating, Investing, Financing }
}

public sealed class GenerateEquityHandler : IRequestHandler<GenerateEquityStatementCommand, EquityResultDto>
{
    private readonly IAccountingDbContext _ctx;
    private readonly ITenantContext _tenant;

    public GenerateEquityHandler(IAccountingDbContext ctx, ITenantContext tenant)
    {
        _ctx = ctx;
        _tenant = tenant;
    }

    public async Task<EquityResultDto> Handle(GenerateEquityStatementCommand request, CancellationToken ct)
    {
        var companyId = _tenant.TenantId ?? throw new InvalidOperationException("Tenant no resuelto.");
        var from = new DateTime(request.FiscalYear, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var to = from.AddYears(1);

        var equityLines = await _ctx.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                        && l.JournalEntry.Date < to
                        && l.Account!.Code.StartsWith("1"))
            .ToListAsync(ct);

        var beginning = equityLines
            .Where(l => l.JournalEntry!.Date < from)
            .Sum(l => l.Credit - l.Debit);

        var yearMovement = equityLines
            .Where(l => l.JournalEntry!.Date >= from)
            .Sum(l => l.Credit - l.Debit);

        var incomeLines = await _ctx.JournalEntryLines
            .Include(l => l.Account)
            .Include(l => l.JournalEntry)
            .AsNoTracking()
            .Where(l => l.JournalEntry!.CompanyId == companyId
                        && l.JournalEntry.Date >= from
                        && l.JournalEntry.Date < to
                        && (l.Account!.Type == "Income" || l.Account.Type == "Expense"))
            .ToListAsync(ct);

        var netIncome = incomeLines
            .Where(l => l.Account!.Type == "Income")
            .Sum(l => l.Credit - l.Debit)
            - incomeLines.Where(l => l.Account!.Type == "Expense").Sum(l => l.Debit - l.Credit);

        var ending = beginning + yearMovement;
        var statement = new EquityStatement
        {
            Id = Guid.NewGuid(),
            CompanyId = companyId,
            FiscalYear = request.FiscalYear,
            CapitalStock = Math.Round(beginning, 2),
            NetIncome = Math.Round(netIncome, 2),
            TotalEquity = Math.Round(ending, 2)
        };

        _ctx.EquityStatements.Add(statement);
        await _ctx.SaveChangesAsync(ct);

        return new EquityResultDto(
            statement.Id,
            request.FiscalYear,
            Math.Round(beginning, 2),
            Math.Round(netIncome, 2),
            0,
            Math.Round(yearMovement - netIncome, 2),
            Math.Round(ending, 2),
            "Generated");
    }
}

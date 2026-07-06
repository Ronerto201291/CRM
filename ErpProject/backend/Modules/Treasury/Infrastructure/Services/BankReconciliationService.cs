using Erp.Application.Common.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Infrastructure.Services;

/// <summary>
/// Algoritmo de conciliación bancaria (ADR-0018 #19c — sin IAccountingDbContext directo).
/// </summary>
public class BankReconciliationService : IBankReconciliationService
{
    private readonly ITreasuryDbContext _ctx;
    private readonly IBankReconciliationLedgerQuery _ledger;
    private readonly ITenantContext _tenant;
    private readonly ILogger<BankReconciliationService> _log;

    public BankReconciliationService(
        ITreasuryDbContext ctx,
        IBankReconciliationLedgerQuery ledger,
        ITenantContext tenant,
        ILogger<BankReconciliationService> log)
    {
        _ctx = ctx;
        _ledger = ledger;
        _tenant = tenant;
        _log = log;
    }

    public async Task<ReconciliationResult> ReconcileAsync(
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        var movements = await _ctx.BankMovements
            .Where(m => m.BankAccountId == bankAccountId
                     && !m.IsReconciled
                     && m.Origin != "System")
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        var bankAccount = await _ctx.BankAccounts
            .AsNoTracking()
            .FirstOrDefaultAsync(b => b.Id == bankAccountId, ct);

        var accountPrefix = bankAccount?.AccountingAccountCode ?? "572";
        var ledgerLines = await _ledger.GetPostedBankLinesAsync(
            companyId, accountPrefix, DateTime.UtcNow.Year - 1, ct);

        var unmatched = ledgerLines.ToList();
        var result = new ReconciliationResult();
        var batchId = Guid.NewGuid();

        foreach (var movement in movements)
        {
            var match = FindMatch(movement, unmatched);
            if (match == null) continue;

            movement.IsReconciled = true;
            movement.MatchedJournalEntryLineId = match.LineId;
            movement.ReconciliationBatchId = batchId;
            result.MatchedCount++;
            result.MatchedAmount += Math.Abs(movement.Amount);
            unmatched.Remove(match);
        }

        if (result.MatchedCount > 0)
        {
            _ctx.ReconciliationBatches.Add(new ReconciliationBatch
            {
                Id = batchId,
                CompanyId = companyId,
                BankAccountId = bankAccountId,
                ReconciledAt = DateTime.UtcNow,
                ItemsCount = result.MatchedCount,
                TotalAmount = result.MatchedAmount,
                Type = "Auto"
            });
            await _ctx.SaveChangesAsync(ct);
            _log.LogInformation("Conciliación {BatchId}: {Count} movimientos, {Amount:F2} €",
                batchId, result.MatchedCount, result.MatchedAmount);
        }

        return result;
    }

    public async Task<List<BankMovement>> ImportFromCsvAsync(
        Guid bankAccountId,
        Stream csvStream,
        CancellationToken ct = default)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        using var reader = new StreamReader(csvStream);
        var movements = new List<BankMovement>();
        var lineNumber = 0;

        while (await reader.ReadLineAsync(ct) is { } line)
        {
            lineNumber++;
            if (lineNumber == 1) continue;

            var parts = line.Split(',');
            if (parts.Length < 3) continue;
            if (!DateTime.TryParse(parts[0].Trim(), out var date)) continue;
            if (!decimal.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)) continue;

            var description = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var reference = parts.Length > 3 ? parts[3].Trim() : $"IMP-{lineNumber}";

            movements.Add(new BankMovement
            {
                Id = Guid.NewGuid(),
                CompanyId = companyId,
                BankAccountId = bankAccountId,
                Date = date,
                Amount = amount,
                Type = amount >= 0 ? "Credit" : "Debit",
                Description = description,
                Reference = reference,
                Origin = "BankImport",
                OriginalBankRef = reference,
                IsReconciled = false,
                CreatedAt = DateTime.UtcNow
            });
        }

        if (movements.Count > 0)
        {
            await _ctx.BankMovements.AddRangeAsync(movements, ct);
            await _ctx.SaveChangesAsync(ct);
        }

        _log.LogInformation("Importados {Count} movimientos para cuenta {AccountId}",
            movements.Count, bankAccountId);
        return movements;
    }

    private static BankLedgerLineDto? FindMatch(BankMovement movement, List<BankLedgerLineDto> candidates)
    {
        var movementAbs = Math.Abs(movement.Amount);

        var match = candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && l.EntryDate.Date == movement.Date.Date);
        if (match != null) return match;

        var invoiceRefs = ExtractInvoiceRefs(movement.Description);
        if (invoiceRefs.Count > 0)
        {
            match = candidates.FirstOrDefault(l =>
                Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
                && invoiceRefs.Any(r =>
                    (l.EntryDescription?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (l.AccountName?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)));
            if (match != null) return match;
        }

        return candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && Math.Abs((l.EntryDate.Date - movement.Date.Date).TotalDays) <= 3);
    }

    private static List<string> ExtractInvoiceRefs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];
        var refs = new List<string>();
        foreach (System.Text.RegularExpressions.Match m in
            System.Text.RegularExpressions.Regex.Matches(text, @"[A-Z]*-?\d{4,}"))
            refs.Add(m.Value);
        return refs;
    }
}

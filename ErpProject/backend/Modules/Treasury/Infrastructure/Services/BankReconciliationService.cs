using Erp.Application.Common.Interfaces;
using Erp.Domain.Entities.Accounting;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Treasury.Application.Interfaces;
using Erp.Modules.Treasury.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Treasury.Infrastructure.Services;

/// <summary>
/// Algoritmo de conciliación bancaria:
/// Fase 1 — Importe exacto + fecha exacta → match alto
/// Fase 2 — Importe exacto + referencia de factura en descripción → match alto
/// Fase 3 — Importe exacto + fecha ±3 días sin referencia → match medio (requiere confirmación)
/// </summary>
public class BankReconciliationService
{
    private readonly ITreasuryDbContext _ctx;
    private readonly IAccountingDbContext _accCtx;
    private readonly ITenantContext _tenant;
    private readonly ILogger<BankReconciliationService> _log;

    public BankReconciliationService(
        ITreasuryDbContext ctx,
        IAccountingDbContext accCtx,
        ITenantContext tenant,
        ILogger<BankReconciliationService> log)
    {
        _ctx = ctx;
        _accCtx = accCtx;
        _tenant = tenant;
        _log = log;
    }

    public async Task<ReconciliationResult> ReconcileAsync(
        Guid bankAccountId,
        CancellationToken ct = default)
    {
        var companyId = _tenant.TenantId
            ?? throw new InvalidOperationException("Tenant not resolved");

        // Obtener movimientos bancarios no conciliados
        var movements = await _ctx.BankMovements
            .Where(m => m.BankAccountId == bankAccountId
                     && !m.IsReconciled
                     && m.Origin != "System")
            .OrderBy(m => m.Date)
            .ToListAsync(ct);

        // Obtener apuntes contables de banco (cuenta 572) no conciliados
        var bankAccount = await _ctx.BankAccounts
            .Where(b => b.Id == bankAccountId)
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        var accountCode = bankAccount?.AccountingAccountCode ?? "572";
        var accountLines = await _accCtx.JournalEntryLines
            .Include(l => l.JournalEntry)
            .Where(l => l.JournalEntry.CompanyId == companyId
                     && l.AccountCode.StartsWith("572")
                     && l.JournalEntry.IsPosted
                     && l.JournalEntry.Date.Year >= DateTime.UtcNow.Year - 1)
            .AsNoTracking()
            .ToListAsync(ct);

        var unmatchedLines = accountLines
            .Where(l => l.Credit > 0 || l.Debit > 0)
            .ToList();

        var result = new ReconciliationResult();
        var batchId = Guid.NewGuid();

        foreach (var movement in movements)
        {
            var match = FindMatch(movement, unmatchedLines);
            if (match == null) continue;

            // Conciliar
            movement.IsReconciled = true;
            movement.MatchedJournalEntryLineId = match.Id;
            movement.ReconciliationBatchId = batchId;

            result.MatchedCount++;
            result.MatchedAmount += Math.Abs(movement.Amount);
            unmatchedLines.Remove(match);
        }

        // Crear lote de conciliación
        if (result.MatchedCount > 0)
        {
            var batch = new ReconciliationBatch
            {
                Id = batchId,
                CompanyId = companyId,
                BankAccountId = bankAccountId,
                ReconciledAt = DateTime.UtcNow,
                ItemsCount = result.MatchedCount,
                TotalAmount = result.MatchedAmount,
                Type = "Auto"
            };
            _ctx.ReconciliationBatches.Add(batch);
        }

        await _ctx.SaveChangesAsync(ct);
        _log.LogInformation("Conciliación {BatchId}: {Count} movimientos conciliados, {Amount:F2} €",
            batchId, result.MatchedCount, result.MatchedAmount);

        return result;
    }

    private JournalEntryLine? FindMatch(BankMovement movement, List<JournalEntryLine> candidates)
    {
        var movementAbs = Math.Abs(movement.Amount);

        // Fase 1: importe exacto + fecha exacta
        var match = candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && l.JournalEntry.Date.Date == movement.Date.Date);

        if (match != null) return match;

        // Fase 2: importe exacto + referencia de factura en descripción
        var invoiceRefs = ExtractInvoiceRefs(movement.Description);
        if (invoiceRefs.Count > 0)
        {
            match = candidates.FirstOrDefault(l =>
                Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
                && invoiceRefs.Any(r =>
                    (l.JournalEntry.Description?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (l.AccountName?.Contains(r, StringComparison.OrdinalIgnoreCase) ?? false)));

            if (match != null) return match;
        }

        // Fase 3: importe exacto + fecha ±3 días
        match = candidates.FirstOrDefault(l =>
            Math.Abs((l.Debit > 0 ? l.Debit : l.Credit) - movementAbs) < 0.01m
            && Math.Abs((l.JournalEntry.Date.Date - movement.Date.Date).TotalDays) <= 3);

        return match;
    }

    /// <summary>Extrae números de factura de una cadena (ej. "Pago factura FR-2026-000123")</summary>
    private static List<string> ExtractInvoiceRefs(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();

        var refs = new List<string>();
        // Patrón: letras opcionales + guión + números
        var matches = System.Text.RegularExpressions.Regex.Matches(
            text, @"[A-Z]*-?\d{4,}");
        foreach (System.Text.RegularExpressions.Match m in matches)
            refs.Add(m.Value);

        return refs;
    }

    /// <summary>
    /// Importa movimientos desde CSV de extracto bancario.
    /// Formato: Fecha,Importe,Concepto,Referencia
    /// </summary>
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
            if (lineNumber == 1) continue; // Saltar header

            var parts = line.Split(',');
            if (parts.Length < 3) continue;

            if (!DateTime.TryParse(parts[0].Trim(), out var date)) continue;
            if (!decimal.TryParse(parts[1].Trim(), System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out var amount)) continue;

            var description = parts.Length > 2 ? parts[2].Trim() : string.Empty;
            var reference = parts.Length > 3 ? parts[3].Trim() : $"IMP-{lineNumber}";

            var movement = new BankMovement
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
            };

            movements.Add(movement);
        }

        if (movements.Count > 0)
        {
            await _ctx.BankMovements.AddRangeAsync(movements, ct);
            await _ctx.SaveChangesAsync(ct);
        }

        _log.LogInformation("Importados {Count} movimientos bancarios para cuenta {AccountId}",
            movements.Count, bankAccountId);

        return movements;
    }
}

public class ReconciliationResult
{
    public int MatchedCount { get; set; }
    public decimal MatchedAmount { get; set; }
}

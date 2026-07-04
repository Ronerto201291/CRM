using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Reacciona a CashSessionClosedEvent (publicado por CloseCashSessionHandler
/// en Treasury cuando un arqueo de caja cierra con diferencia, ADR-0018 #42b)
/// registrando el ajuste contable. Treasury nunca crea asientos directamente.
///
/// - Sobra (Difference > 0): Debe 570 (Caja), Haber 778 (Ingresos excepcionales).
/// - Falta (Difference &lt; 0): Debe 668 (Otras pérdidas en gestión corriente), Haber 570 (Caja).
///
/// Idempotencia: JournalEntry.SourceType="CashSessionDifference", SourceId=CashSessionId.
/// </summary>
public class PostCashDifferenceHandler : INotificationHandler<CashSessionClosedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<PostCashDifferenceHandler> _logger;

    public PostCashDifferenceHandler(IAccountingDbContext context, ILogger<PostCashDifferenceHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(CashSessionClosedEvent notification, CancellationToken ct)
    {
        if (notification.Difference == 0) return;

        var exists = await _context.JournalEntries
            .AnyAsync(je => je.SourceType == "CashSessionDifference" && je.SourceId == notification.CashSessionId, ct);
        if (exists)
        {
            _logger.LogWarning("CashSessionClosedEvent already processed for session {CashSessionId}, skipping.", notification.CashSessionId);
            return;
        }

        var cash = await GetAccount(notification.CompanyId, "570", ct)
            ?? throw new InvalidOperationException("Account 570 (Caja) not found");
        var isSurplus = notification.Difference > 0;
        var adjustmentCode = isSurplus ? "778" : "668";
        var adjustment = await GetAccount(notification.CompanyId, adjustmentCode, ct)
            ?? throw new InvalidOperationException($"Account {adjustmentCode} not found");

        var amount = Math.Abs(notification.Difference);
        var journalEntry = new JournalEntry
        {
            Id = Guid.NewGuid(),
            CompanyId = notification.CompanyId,
            Date = notification.ClosedAt,
            Reference = $"ARQUEO-{notification.CashSessionId.ToString()[..8]}",
            Description = isSurplus
                ? $"Sobrante de caja en arqueo ({amount:F2} €)"
                : $"Faltante de caja en arqueo ({amount:F2} €)",
            SourceType = "CashSessionDifference",
            SourceId = notification.CashSessionId,
            IsPosted = true,
            PostedAt = DateTime.UtcNow,
            JournalEntryLines = isSurplus
                ? new List<JournalEntryLine>
                {
                    new() { Id = Guid.NewGuid(), AccountId = cash.Id, AccountCode = cash.Code, AccountName = cash.Name, Debit = amount, Credit = 0 },
                    new() { Id = Guid.NewGuid(), AccountId = adjustment.Id, AccountCode = adjustment.Code, AccountName = adjustment.Name, Debit = 0, Credit = amount },
                }
                : new List<JournalEntryLine>
                {
                    new() { Id = Guid.NewGuid(), AccountId = adjustment.Id, AccountCode = adjustment.Code, AccountName = adjustment.Name, Debit = amount, Credit = 0 },
                    new() { Id = Guid.NewGuid(), AccountId = cash.Id, AccountCode = cash.Code, AccountName = cash.Name, Debit = 0, Credit = amount },
                },
        };

        foreach (var line in journalEntry.JournalEntryLines)
            line.JournalEntryId = journalEntry.Id;

        _context.JournalEntries.Add(journalEntry);
        await _context.SaveChangesAsync(ct);

        _logger.LogInformation("Journal entry {EntryId} created for cash session difference {CashSessionId} ({Amount:F2} €, {Kind})",
            journalEntry.Id, notification.CashSessionId, amount, isSurplus ? "sobra" : "falta");
    }

    private async Task<Account?> GetAccount(Guid companyId, string code, CancellationToken ct)
        => await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}

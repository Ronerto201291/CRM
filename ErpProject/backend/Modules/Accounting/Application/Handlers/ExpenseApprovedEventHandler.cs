using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Application.Interfaces;
using Erp.Modules.Accounting.Domain.Entities;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Handles ExpenseApprovedEvent to automatically generate accounting entries.
/// This ensures expenses are immediately reflected in the GL.
///
/// Accounting Rules (Spanish PGC):
/// - Debit: 600 (Purchases) + 472 (VAT Deductible) + 4700 (Deductible IRPF)
/// - Credit: 410 (Suppliers)
/// </summary>
public class ExpenseApprovedEventHandler : INotificationHandler<ExpenseApprovedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<ExpenseApprovedEventHandler> _logger;

    public ExpenseApprovedEventHandler(
        IAccountingDbContext context,
        ILogger<ExpenseApprovedEventHandler> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task Handle(ExpenseApprovedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Idempotency: skip if journal entry already created for this expense document
            var exists = await _context.JournalEntries
                .AnyAsync(je => je.SourceType == "Expense" && je.SourceId == notification.ExpenseDocumentId, cancellationToken);
            if (exists)
            {
                _logger.LogWarning("ExpenseApprovedEvent already processed for {DocumentId}, skipping.", notification.ExpenseDocumentId);
                return;
            }

            _logger.LogInformation(
                "Processing ExpenseApprovedEvent for document {DocumentId} (Company: {CompanyId})",
                notification.ExpenseDocumentId, notification.CompanyId);

            var journalEntry = new JournalEntry
            {
                Id = Guid.NewGuid(),
                CompanyId = notification.CompanyId,
                Date = notification.IssueDate,
                Reference = $"GASTO-{notification.ExpenseDocumentId.ToString()[..8]}",
                Description = $"Asiento automático gasto {notification.SupplierName}",
                SourceType = "Expense",
                SourceId = notification.ExpenseDocumentId,
                IsPosted = true,
                PostedAt = DateTime.UtcNow,
                JournalEntryLines = new List<JournalEntryLine>()
            };

            var acct600 = await GetAccount(notification.CompanyId, "600", cancellationToken)
                ?? throw new InvalidOperationException("Account 600 (Compras) not found");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct600.Id, AccountCode = acct600.Code, AccountName = acct600.Name,
                Debit = notification.TaxBase, Credit = 0
            });

            if (notification.VATAmount > 0)
            {
                var acct472 = await GetAccount(notification.CompanyId, "472", cancellationToken)
                    ?? throw new InvalidOperationException("Account 472 (HP IVA Soportado) not found");
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                    AccountId = acct472.Id, AccountCode = acct472.Code, AccountName = acct472.Name,
                    Debit = notification.VATAmount, Credit = 0
                });
            }

            if (notification.IRPFAmount.HasValue && notification.IRPFAmount > 0)
            {
                var acct4700 = await GetAccount(notification.CompanyId, "4700", cancellationToken)
                    ?? throw new InvalidOperationException("Account 4700 (IRPF Deducible) not found");
                journalEntry.JournalEntryLines.Add(new JournalEntryLine
                {
                    Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                    AccountId = acct4700.Id, AccountCode = acct4700.Code, AccountName = acct4700.Name,
                    Debit = notification.IRPFAmount.Value, Credit = 0
                });
            }

            var acct410 = await GetAccount(notification.CompanyId, "410", cancellationToken)
                ?? throw new InvalidOperationException("Account 410 (Proveedores) not found");
            journalEntry.JournalEntryLines.Add(new JournalEntryLine
            {
                Id = Guid.NewGuid(), JournalEntryId = journalEntry.Id,
                AccountId = acct410.Id, AccountCode = acct410.Code, AccountName = acct410.Name,
                Debit = 0, Credit = notification.Total
            });

            var totalDebits  = journalEntry.JournalEntryLines.Sum(l => l.Debit);
            var totalCredits = journalEntry.JournalEntryLines.Sum(l => l.Credit);
            if (Math.Abs(totalDebits - totalCredits) > 0.01m)
                throw new InvalidOperationException(
                    $"Asiento descuadrado: Debe={totalDebits:F2} ≠ Haber={totalCredits:F2}");

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Journal entry {JournalEntryId} created for expense {ExpenseDocumentId}",
                journalEntry.Id, notification.ExpenseDocumentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing ExpenseApprovedEvent for document {DocumentId}",
                notification.ExpenseDocumentId);
            throw;
        }
    }

    private async Task<Account?> GetAccount(Guid companyId, string code, CancellationToken ct)
        => await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}

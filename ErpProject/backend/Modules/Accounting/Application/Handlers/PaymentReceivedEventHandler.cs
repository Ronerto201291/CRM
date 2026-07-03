using Erp.Application.Common.Events;
using Erp.Modules.Accounting.Domain.Entities;
using Erp.Modules.Accounting.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Erp.Modules.Accounting.Application.Handlers;

/// <summary>
/// Handles PaymentReceivedEvent to close the client receivable in the GL.
///
/// Accounting Rules (Spanish PGC):
/// - Debit:  572 (Bancos c/c) or 570 (Caja) = Amount
/// - Credit: 430 (Clientes)                  = Amount
///
/// Idempotency key: JournalEntry.SourceType="Payment", SourceId=InvoiceId
/// (PaymentId == InvoiceId by design in MarkPaidHandler)
/// </summary>
public class PaymentReceivedEventHandler : INotificationHandler<PaymentReceivedEvent>
{
    private readonly IAccountingDbContext _context;
    private readonly ILogger<PaymentReceivedEventHandler> _logger;

    public PaymentReceivedEventHandler(
        IAccountingDbContext context,
        ILogger<PaymentReceivedEventHandler> logger)
    {
        _context = context;
        _logger  = logger;
    }

    public async Task Handle(PaymentReceivedEvent notification, CancellationToken cancellationToken)
    {
        try
        {
            // Idempotency: skip if journal entry already created for this payment
            var exists = await _context.JournalEntries
                .AnyAsync(je => je.SourceType == "Payment" && je.SourceId == notification.InvoiceId, cancellationToken);
            if (exists)
            {
                _logger.LogWarning("PaymentReceivedEvent already processed for invoice {InvoiceId}, skipping.", notification.InvoiceId);
                return;
            }

            _logger.LogInformation(
                "Processing PaymentReceivedEvent for invoice {InvoiceNumber} (Company: {CompanyId})",
                notification.InvoiceNumber, notification.CompanyId);

            // 572 Bancos c/c (default) or 570 Caja for cash payments
            var bankAccountCode = notification.PaymentMethod == "cash" ? "570" : "572";
            var bankAccount = await GetAccount(notification.CompanyId, bankAccountCode, cancellationToken)
                ?? throw new InvalidOperationException($"Account {bankAccountCode} not found");

            var acct430 = await GetAccount(notification.CompanyId, "430", cancellationToken)
                ?? throw new InvalidOperationException("Account 430 (Clientes) not found");

            var journalEntry = new JournalEntry
            {
                Id          = Guid.NewGuid(),
                CompanyId   = notification.CompanyId,
                Date        = notification.PaymentDate,
                Reference   = $"COBRO-{notification.InvoiceNumber}",
                Description = $"Cobro factura {notification.InvoiceNumber} ({notification.PaymentMethod})",
                SourceType  = "Payment",
                SourceId    = notification.InvoiceId,
                IsPosted    = true,
                PostedAt    = DateTime.UtcNow,
                JournalEntryLines = new List<JournalEntryLine>
                {
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AccountId   = bankAccount.Id,
                        AccountCode = bankAccount.Code,
                        AccountName = bankAccount.Name,
                        Debit  = notification.Amount,
                        Credit = 0
                    },
                    new()
                    {
                        Id = Guid.NewGuid(),
                        AccountId   = acct430.Id,
                        AccountCode = acct430.Code,
                        AccountName = acct430.Name,
                        Debit  = 0,
                        Credit = notification.Amount
                    }
                }
            };

            // Wire JournalEntryId on lines (required by FK)
            foreach (var line in journalEntry.JournalEntryLines)
                line.JournalEntryId = journalEntry.Id;

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Journal entry {EntryId} created for payment of invoice {InvoiceNumber}",
                journalEntry.Id, notification.InvoiceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing PaymentReceivedEvent for invoice {InvoiceId}", notification.InvoiceId);
            throw;
        }
    }

    private async Task<Account?> GetAccount(Guid companyId, string code, CancellationToken ct)
        => await _context.Accounts
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CompanyId == companyId && a.Code == code, ct);
}
